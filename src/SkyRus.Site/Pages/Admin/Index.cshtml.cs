using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class IndexModel(CurrentUser me) : AdminPageModel(me)
{
    public IActionResult OnGet() => Redirect("/admin/staff");
}
