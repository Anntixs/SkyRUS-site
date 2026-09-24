using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages;

[EnableRateLimiting("login")]
public sealed class LoginModel(INetworkClient network, UserService users) : PageModel
{
    [BindProperty] public string Cid { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; private set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/cabinet") : Page();

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!long.TryParse(Cid.Trim(), out var cid) || cid <= 0 || Password.Length == 0)
        {
            Error = "Введите CID и пароль SkyNetwork";
            return Page();
        }
        var result = await network.AuthenticateAsync(cid, Password, HttpContext.RequestAborted);
        switch (result.Outcome)
        {
            case AuthOutcome.Ok:
                users.Upsert(result.Member!, login: true);
                await CurrentUser.SignInAsync(HttpContext, cid);
                return LocalRedirect(returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/cabinet");
            case AuthOutcome.WrongPassword: Error = "Неверный CID или пароль"; break;
            case AuthOutcome.Suspended: Error = "Аккаунт заблокирован в SkyNetwork"; break;
            case AuthOutcome.TooManyAttempts: Error = "Слишком много неудачных попыток. Попробуйте через несколько минут"; break;
            default: Error = "Сервер SkyNetwork недоступен, попробуйте позже"; break;
        }
        return Page();
    }
}
