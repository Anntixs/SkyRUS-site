using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SkyRus.Site.Network;

public sealed record NetworkMember(long Cid, string Name, string Email, string Rating, string PilotRating);

public enum AuthOutcome { Ok, WrongPassword, Suspended, TooManyAttempts, Unavailable }

public sealed record AuthResult(AuthOutcome Outcome, NetworkMember? Member = null);

public sealed record RatingRequestPayload(
    long Cid, string Track, string Rating, long ExaminerCid, string ExaminerName, string ExamDate,
    string Score, string ReportUrl, string Comment, string ExternalId);

/// <summary>A rating request as the network sees it, or why it could not be sent / read.</summary>
public sealed record RatingRequestState(long? Id, string Status, string ReviewComment, string? Error);

/// <summary>The network's division API (/api/division/v1), authenticated with the division key.</summary>
public interface INetworkClient
{
    Task<AuthResult> AuthenticateAsync(long cid, string password, CancellationToken ct = default);
    Task<NetworkMember?> MemberAsync(long cid, CancellationToken ct = default);
    Task<RatingRequestState> SendRatingRequestAsync(RatingRequestPayload request, CancellationToken ct = default);
    Task<RatingRequestState> RatingRequestAsync(long id, CancellationToken ct = default);
}

public sealed class HttpNetworkClient(IHttpClientFactory factory, IOptions<SiteOptions> options, ILogger<HttpNetworkClient> log) : INetworkClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient? Client()
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.NetworkUrl) || string.IsNullOrWhiteSpace(o.NetworkApiKey)) return null;
        var c = factory.CreateClient("network");
        c.BaseAddress = new Uri(o.NetworkUrl.TrimEnd('/') + "/api/division/v1/");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o.NetworkApiKey.Trim());
        return c;
    }

    private sealed record MemberDto(long Cid, string? Name, string? Email, string? Rating, string? PilotRating);
    private sealed record RequestDto(long Id, string? Status, string? ReviewComment);
    private sealed record ErrorDto(string? Error, string? Message);

    private static NetworkMember ToMember(MemberDto m) =>
        new(m.Cid, m.Name ?? "", m.Email ?? "", m.Rating ?? "OBS", m.PilotRating ?? "");

    public async Task<AuthResult> AuthenticateAsync(long cid, string password, CancellationToken ct = default)
    {
        if (Client() is not { } c) return new(AuthOutcome.Unavailable);
        try
        {
            using var r = await c.PostAsJsonAsync("auth", new { cid, password }, Json, ct);
            return r.StatusCode switch
            {
                HttpStatusCode.OK => new(AuthOutcome.Ok, ToMember((await r.Content.ReadFromJsonAsync<MemberDto>(Json, ct))!)),
                HttpStatusCode.Unauthorized when (await Error(r, ct)) == "invalid_credentials" => new(AuthOutcome.WrongPassword),
                HttpStatusCode.Forbidden => new(AuthOutcome.Suspended),
                HttpStatusCode.TooManyRequests => new(AuthOutcome.TooManyAttempts),
                _ => Fail(r.StatusCode),
            };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Network sign-in failed");
            return new(AuthOutcome.Unavailable);
        }

        AuthResult Fail(HttpStatusCode code)
        {
            log.LogWarning("Network sign-in: HTTP {Code} (check Site:NetworkApiKey)", (int)code);
            return new(AuthOutcome.Unavailable);
        }
    }

    public async Task<NetworkMember?> MemberAsync(long cid, CancellationToken ct = default)
    {
        if (Client() is not { } c) return null;
        try
        {
            using var r = await c.GetAsync($"members/{cid}", ct);
            return r.IsSuccessStatusCode ? ToMember((await r.Content.ReadFromJsonAsync<MemberDto>(Json, ct))!) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Network member {Cid} lookup failed", cid);
            return null;
        }
    }

    public async Task<RatingRequestState> SendRatingRequestAsync(RatingRequestPayload request, CancellationToken ct = default)
    {
        if (Client() is not { } c) return new(null, "", "", "API сети не настроен (Site:NetworkUrl, Site:NetworkApiKey)");
        try
        {
            using var r = await c.PostAsJsonAsync("rating-requests", request, Json, ct);
            if (r.IsSuccessStatusCode)
            {
                var dto = (await r.Content.ReadFromJsonAsync<RequestDto>(Json, ct))!;
                return new(dto.Id, dto.Status ?? "pending", dto.ReviewComment ?? "", null);
            }
            var err = await r.Content.ReadFromJsonAsync<ErrorDto>(Json, ct);
            return new(null, "", "", $"{(int)r.StatusCode} {err?.Error}: {err?.Message}".Trim());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(null, "", "", "сеть недоступна: " + e.Message);
        }
    }

    public async Task<RatingRequestState> RatingRequestAsync(long id, CancellationToken ct = default)
    {
        if (Client() is not { } c) return new(id, "", "", "API сети не настроен");
        try
        {
            using var r = await c.GetAsync($"rating-requests/{id}", ct);
            if (!r.IsSuccessStatusCode) return new(id, "", "", $"HTTP {(int)r.StatusCode}");
            var dto = (await r.Content.ReadFromJsonAsync<RequestDto>(Json, ct))!;
            return new(dto.Id, dto.Status ?? "", dto.ReviewComment ?? "", null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(id, "", "", "сеть недоступна: " + e.Message);
        }
    }

    private static async Task<string?> Error(HttpResponseMessage r, CancellationToken ct)
    {
        try { return (await r.Content.ReadFromJsonAsync<ErrorDto>(Json, ct))?.Error; }
        catch (JsonException) { return null; }
    }
}
