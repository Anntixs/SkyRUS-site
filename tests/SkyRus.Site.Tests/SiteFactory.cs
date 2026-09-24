using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SkyRus.Site.Network;

namespace SkyRus.Site.Tests;

/// <summary>The network's division API, in memory.</summary>
public sealed class FakeNetwork : INetworkClient
{
    public ConcurrentDictionary<long, (string Password, NetworkMember Member)> Members { get; } = new();
    public List<RatingRequestPayload> Sent { get; } = [];
    public ConcurrentDictionary<long, (string Status, string Comment)> Requests { get; } = new();
    public bool Down { get; set; }

    public long Add(long cid, string name, string rating = "OBS", string password = "password1")
    {
        Members[cid] = (password, new NetworkMember(cid, name, $"{cid}@example.com", rating, "P0"));
        return cid;
    }

    public Task<AuthResult> AuthenticateAsync(long cid, string password, CancellationToken ct = default) =>
        Task.FromResult(Down ? new AuthResult(AuthOutcome.Unavailable)
            : Members.TryGetValue(cid, out var m) && m.Password == password ? new AuthResult(AuthOutcome.Ok, m.Member)
            : new AuthResult(AuthOutcome.WrongPassword));

    public Task<NetworkMember?> MemberAsync(long cid, CancellationToken ct = default) =>
        Task.FromResult(!Down && Members.TryGetValue(cid, out var m) ? m.Member : null);

    public Task<RatingRequestState> SendRatingRequestAsync(RatingRequestPayload request, CancellationToken ct = default)
    {
        if (Down) return Task.FromResult(new RatingRequestState(null, "", "", "сеть недоступна"));
        lock (Sent) Sent.Add(request);
        long id = 100 + Sent.Count;
        Requests[id] = ("pending", "");
        return Task.FromResult(new RatingRequestState(id, "pending", "", null));
    }

    public Task<RatingRequestState> RatingRequestAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Down || !Requests.TryGetValue(id, out var r)
            ? new RatingRequestState(id, "", "", "нет")
            : new RatingRequestState(id, r.Status, r.Comment, null));

    /// <summary>A supervisor decides; the member's rating changes on approval.</summary>
    public void Decide(long id, string status, string comment, long cid, string newRating)
    {
        Requests[id] = (status, comment);
        if (status == "approved" && Members.TryGetValue(cid, out var m))
            Members[cid] = (m.Password, m.Member with { Rating = newRating });
    }
}

/// <summary>The site on a fresh temporary database with the fake network.</summary>
public sealed class SiteFactory : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"skyrus-{Guid.NewGuid():N}.db");
    public FakeNetwork Network { get; } = new();
    public string Admins { get; init; } = "1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Site:Database", DatabasePath);
        builder.UseSetting("Site:Admins", Admins);
        builder.UseSetting("Site:PublicUrl", "https://skyrus.example");
        builder.UseSetting("Site:LoginAttemptsPerMinute", "10000");
        builder.UseEnvironment("Production");
        // No background sync during tests: they run it directly.
        builder.UseSetting("Site:BackgroundSync", "false");
        builder.ConfigureTestServices(s => s.AddSingleton<INetworkClient>(Network));
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>A signed-in browser for a network member (created in the fake network if needed).</summary>
    public async Task<HttpClient> As(long cid, string name = "", string rating = "OBS")
    {
        if (!Network.Members.ContainsKey(cid)) Network.Add(cid, name.Length > 0 ? name : $"Member {cid}", rating);
        var c = Browser();
        var r = await c.SubmitAsync("/login", new Dictionary<string, string> { ["Cid"] = cid.ToString(), ["Password"] = Network.Members[cid].Password });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
            try { File.Delete(f); } catch (IOException) { }
    }
}

public static class BrowserExtensions
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");

    /// <summary>GETs the page, then posts the fields with the page's antiforgery token.</summary>
    public static async Task<HttpResponseMessage> SubmitAsync(this HttpClient c, string page, IDictionary<string, string> fields, string? action = null)
    {
        var html = await (await c.GetAsync(page)).Content.ReadAsStringAsync();
        var m = Token.Match(html);
        Assert.True(m.Success, $"no form on {page}");
        var data = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(m.Groups[1].Value) };
        return await c.PostAsync(action ?? page, new FormUrlEncodedContent(data));
    }

    public static async Task<string> HtmlAsync(this HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url}: {(int)r.StatusCode}\n{body[..Math.Min(body.Length, 3000)]}");
        return body;
    }

    public static async Task<string> TextAsync(this Task<HttpResponseMessage> r) => await (await r).Content.ReadAsStringAsync();
}
