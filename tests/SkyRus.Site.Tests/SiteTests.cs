using System.Net;
using SkyRus.Site.Data;

namespace SkyRus.Site.Tests;

public class SiteTests
{
    [Fact]
    public async Task PublicPages()
    {
        using var site = new SiteFactory();
        var c = site.Browser();
        Assert.Contains("SkyRUS", await c.HtmlAsync("/"));
        var regions = await c.HtmlAsync("/regions");
        Assert.Contains("Иркутск", regions);
        Assert.Contains("UUEE", await c.HtmlAsync("/regions/UUWV"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/regions/XXXX")).StatusCode);
        await c.HtmlAsync("/news");
        await c.HtmlAsync("/permits");
        Assert.Contains("Порядок обучения", await c.HtmlAsync("/documents"));
        var doc = site.Get<SiteContent>().Documents().First();
        await c.HtmlAsync($"/documents/{doc.Id}");
        // Seeded templates: theory, practice and an exam for each stage.
        Assert.Equal(12, site.Get<ProtocolService>().Templates().Count);
    }

    [Fact]
    public async Task SignIn_ThroughSkyNetworkConnect()
    {
        using var site = new SiteFactory();
        site.Network.Add(25, "Petr Student", "S1");
        var c = site.Browser();
        // No password form: only the SkyNetwork button.
        var login = await c.HtmlAsync("/login");
        Assert.Contains("Войти через SkyNetwork", login);
        Assert.DoesNotContain("type=\"password\"", login);

        // The start sends the member to the network with state and a PKCE challenge.
        var start = await c.GetAsync("/login/start?returnUrl=/permits");
        var location = start.Headers.Location!.OriginalString;
        Assert.StartsWith("https://network.example/oauth/authorize?", location);
        Assert.Contains(Uri.EscapeDataString("https://skyrus.example/login/callback"), location);

        // A forged callback (wrong state) is refused.
        var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(location).Query);
        var forged = await c.GetAsync($"/login/callback?code={site.Network.IssueCode(25, q["code_challenge"]!)}&state=forged");
        Assert.Contains("Сеанс входа устарел", await forged.Content.ReadAsStringAsync());

        // The member said no on the network.
        Assert.Equal("/login?error=access_denied", (await c.GetAsync("/login/callback?error=access_denied")).Headers.Location!.OriginalString);

        // The network is down: a clear message.
        site.Network.Down = true;
        var down = await c.SignInAsync(site, 25);
        Assert.Contains("недоступен", await down.Content.ReadAsStringAsync());
        site.Network.Down = false;

        var ok = await c.SignInAsync(site, 25, "/permits");
        Assert.Equal("/permits", ok.Headers.Location!.OriginalString);
        var cabinet = await c.HtmlAsync("/cabinet");
        Assert.Contains("Petr Student", cabinet);
        Assert.Contains("S1", cabinet);

        // Sign-out from the header form (with its antiforgery token).
        var signedOut = await c.SubmitAsync("/", new Dictionary<string, string>(), "/logout");
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/cabinet")).StatusCode);
        await c.SignInAsync(site, 25);

        // The site keeps working without the network: public pages and the signed-in session.
        site.Network.Down = true;
        await site.Browser().HtmlAsync("/regions");
        await c.HtmlAsync("/cabinet");
    }

    [Fact]
    public async Task ClosedSections()
    {
        using var site = new SiteFactory();
        var anonymous = site.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/tc")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/cabinet")).StatusCode);

        var member = await site.As(25, "Petr Student");
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync("/tc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync("/admin/staff")).StatusCode);
        Assert.DoesNotContain("Учебный центр</a>", await member.HtmlAsync("/"));

        // A mentor has the training center but not the administration.
        var admin = await site.As(1, "Anna Admin");
        await admin.SubmitAsync("/admin/staff", new Dictionary<string, string> { ["cid"] = "25", ["role"] = "mentor" });
        await member.HtmlAsync("/tc");
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync("/admin/templates")).StatusCode);
        // Unknown CIDs are checked against the network.
        var unknown = await admin.SubmitAsync("/admin/staff", new Dictionary<string, string> { ["cid"] = "999", ["role"] = "mentor" }).TextAsync();
        Assert.Contains("не найден", unknown);
    }

    [Fact]
    public async Task Admin_EditsContent()
    {
        using var site = new SiteFactory();
        var admin = await site.As(1, "Anna Admin");

        var news = await admin.SubmitAsync("/admin/news/new", new Dictionary<string, string> { ["title"] = "Открыт набор", ["body"] = "Ждём студентов", ["published"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, news.StatusCode);
        Assert.Contains("Открыт набор", await site.Browser().HtmlAsync("/"));

        var badDoc = await admin.SubmitAsync("/admin/documents/new", new Dictionary<string, string>
            { ["title"] = "Файл", ["category"] = "", ["body"] = "", ["url"] = "javascript:alert(1)", ["sort"] = "0" }).TextAsync();
        Assert.Contains("http://", badDoc);

        var t = site.Get<ProtocolService>().Templates().First();
        await admin.SubmitAsync($"/admin/templates/{t.Id}", new Dictionary<string, string>
        {
            ["title"] = t.Title, ["kind"] = t.Kind, ["track"] = "atc", ["targetRating"] = "", ["sort"] = "0", ["active"] = "true",
            ["items"] = "Первый пункт | пояснение\nВторой пункт",
        });
        var edited = site.Get<ProtocolService>().Template(t.Id)!;
        Assert.Equal([("Первый пункт", "пояснение"), ("Второй пункт", "")], edited.Items.Select(i => (i.Title, i.Description)).ToArray());

        var exam = await admin.SubmitAsync("/admin/templates/new", new Dictionary<string, string>
            { ["title"] = "Экзамен без рейтинга", ["kind"] = "exam", ["track"] = "atc", ["targetRating"] = "", ["sort"] = "0", ["items"] = "Пункт" }).TextAsync();
        Assert.Contains("укажите рейтинг", exam);

        var fir = await admin.SubmitAsync("/admin/regions", new Dictionary<string, string> { ["code"] = "uwpp", ["name"] = "Пенза", ["description"] = "" }, "/admin/regions?handler=Fir");
        Assert.NotNull(site.Get<SiteContent>().Fir("UWPP"));
    }
}
