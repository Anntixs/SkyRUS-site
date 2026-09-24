using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SkyRus.Site.Network;

public sealed record NetworkMember(long Cid, string Name, string Email, string Rating, string PilotRating);

/// <summary>The member signed in through SkyNetwork Connect, or why not.</summary>
public sealed record SignInResult(NetworkMember? Member, string? Error);

public sealed record RatingRequestPayload(
    long Cid, string Track, string Rating, long ExaminerCid, string ExaminerName, string ExamDate,
    string Score, string ReportUrl, string Comment, string ExternalId);

/// <summary>A rating request as the network sees it, or why it could not be sent / read.</summary>
public sealed record RatingRequestState(long? Id, string Status, string ReviewComment, string? Error);

/// <summary>
/// The network: SkyNetwork Connect for sign-in (/oauth/…, with the client ID and secret) and the
/// division API (/api/division/v1, with the division key) for members and rating requests.
/// </summary>
public interface INetworkClient
{
    /// <summary>Where to send the member to sign in (the network's /oauth/authorize).</summary>
    string AuthorizeUrl(string redirectUri, string state, string codeChallenge);

    /// <summary>Exchanges the code from the network for the member's profile.</summary>
    Task<SignInResult> SignInAsync(string code, string redirectUri, string codeVerifier, CancellationToken ct = default);

    Task<NetworkMember?> MemberAsync(long cid, CancellationToken ct = default);
    Task<RatingRequestState> SendRatingRequestAsync(RatingRequestPayload request, CancellationToken ct = default);
    Task<RatingRequestState> RatingRequestAsync(long id, CancellationToken ct = default);
}

public sealed class HttpNetworkClient(IHttpClientFactory factory, IOptions<SiteOptions> options, ILogger<HttpNetworkClient> log) : INetworkClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Snake = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

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

    private string NetworkBase => options.Value.NetworkUrl.TrimEnd('/');

    public string AuthorizeUrl(string redirectUri, string state, string codeChallenge) =>
        $"{NetworkBase}/oauth/authorize?response_type=code&client_id={Uri.EscapeDataString(options.Value.ConnectClientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString("profile email")}" +
        $"&state={Uri.EscapeDataString(state)}&code_challenge={codeChallenge}&code_challenge_method=S256";

    private sealed record TokenDto(string? AccessToken);
    private sealed record UserDto(long Cid, string? Name, string? Email, string? Rating, string? PilotRating);

    public async Task<SignInResult> SignInAsync(string code, string redirectUri, string codeVerifier, CancellationToken ct = default)
    {
        var o = options.Value;
        if (o.NetworkUrl.Length == 0 || o.ConnectClientId.Length == 0 || o.ConnectClientSecret.Length == 0)
            return new(null, "Вход через SkyNetwork не настроен (Site:NetworkUrl, Site:ConnectClientId, Site:ConnectClientSecret)");
        try
        {
            var http = factory.CreateClient("network");
            using var tokenResponse = await http.PostAsync($"{NetworkBase}/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirectUri,
                ["client_id"] = o.ConnectClientId, ["client_secret"] = o.ConnectClientSecret, ["code_verifier"] = codeVerifier,
            }), ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                log.LogWarning("Connect token: HTTP {Code} {Body}", (int)tokenResponse.StatusCode, await tokenResponse.Content.ReadAsStringAsync(ct));
                return new(null, "SkyNetwork не подтвердил вход, попробуйте ещё раз");
            }
            var token = await tokenResponse.Content.ReadFromJsonAsync<TokenDto>(Snake, ct);
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{NetworkBase}/oauth/userinfo");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token?.AccessToken);
            using var userResponse = await http.SendAsync(userRequest, ct);
            if (!userResponse.IsSuccessStatusCode) return new(null, "SkyNetwork не выдал данные аккаунта (возможно, аккаунт заблокирован)");
            var u = (await userResponse.Content.ReadFromJsonAsync<UserDto>(Json, ct))!;
            return new(new NetworkMember(u.Cid, u.Name ?? "", u.Email ?? "", u.Rating ?? "OBS", u.PilotRating ?? ""), null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Connect sign-in failed");
            return new(null, "Сервер SkyNetwork недоступен, попробуйте позже");
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
}
