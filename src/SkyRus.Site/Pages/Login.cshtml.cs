using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages;

/// <summary>
/// Sign-in through SkyNetwork Connect: the member is sent to the network, signs in there (the password
/// never reaches this site) and comes back to /login/callback with a one-time code. The state and the
/// PKCE verifier travel in a short-lived encrypted cookie.
/// </summary>
[EnableRateLimiting("login")]
public sealed class LoginModel(INetworkClient network, UserService users, IDataProtectionProvider protection, IOptions<SiteOptions> options) : PageModel
{
    private const string Cookie = "skyrus.signin";
    public string? Error { get; private set; }

    private IDataProtector Protector => protection.CreateProtector("skyrus.signin");

    private string CallbackUrl()
    {
        var baseUrl = options.Value.PublicUrl.TrimEnd('/');
        if (baseUrl.Length == 0) baseUrl = $"{Request.Scheme}://{Request.Host}";
        return baseUrl + "/login/callback";
    }

    private static string Random() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public IActionResult OnGet(string? error)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/cabinet");
        Error = error switch
        {
            null or "" => null,
            "access_denied" => "Вход отменён",
            _ => "Не удалось войти через SkyNetwork, попробуйте ещё раз",
        };
        return Page();
    }

    /// <summary>/login?handler=Start: off to the network.</summary>
    public IActionResult OnGetStart(string? returnUrl)
    {
        string state = Random(), verifier = Random();
        string back = returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/cabinet";
        Response.Cookies.Append(Cookie, Protector.Protect($"{state}\n{verifier}\n{back}"), new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10), IsEssential = true,
        });
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Redirect(network.AuthorizeUrl(CallbackUrl(), state, challenge));
    }

    /// <summary>/login?handler=Callback: back from the network with a code.</summary>
    public async Task<IActionResult> OnGetCallbackAsync(string? code, string? state, string? error)
    {
        string[]? saved = null;
        try
        {
            if (Request.Cookies[Cookie] is { } c) saved = Protector.Unprotect(c).Split('\n');
        }
        catch (CryptographicException) { }
        Response.Cookies.Delete(Cookie);
        if (error != null) return Redirect("/login?error=" + Uri.EscapeDataString(error));
        if (saved is not { Length: 3 } || state == null || state != saved[0] || string.IsNullOrEmpty(code))
        {
            Error = "Сеанс входа устарел. Нажмите «Войти через SkyNetwork» ещё раз";
            return Page();
        }
        var result = await network.SignInAsync(code, CallbackUrl(), saved[1], HttpContext.RequestAborted);
        if (result.Member is not { } m)
        {
            Error = result.Error;
            return Page();
        }
        users.Upsert(m, login: true);
        await CurrentUser.SignInAsync(HttpContext, m.Cid);
        return LocalRedirect(saved[2]);
    }
}
