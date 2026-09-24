using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.News;

public sealed class DetailsModel(SiteContent content, CurrentUser me) : PageModel
{
    public NewsPost Post { get; private set; } = new();

    public IActionResult OnGet(long id)
    {
        if (content.Post(id) is not { } p || (!p.Published && !me.IsAdmin)) return NotFound();
        Post = p;
        return Page();
    }
}
