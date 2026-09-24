using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;

namespace SkyRus.Site.Pages.Documents;

public sealed class DetailsModel(SiteContent content) : PageModel
{
    public Document Doc { get; private set; } = new();

    public IActionResult OnGet(long id)
    {
        if (content.Document(id) is not { } d) return NotFound();
        Doc = d;
        return Page();
    }
}
