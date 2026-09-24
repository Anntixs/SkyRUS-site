using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SkyRus.Site.Pages;

/// <summary>Status code pages; re-executed for any method, so no antiforgery check here.</summary>
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public int Code { get; private set; }

    public void OnGet(int code)
    {
        Code = code;
        Response.StatusCode = code is >= 400 and < 600 ? code : 500;
    }

    public void OnPost(int code) => OnGet(code);
}
