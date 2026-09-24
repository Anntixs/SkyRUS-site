using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

/// <summary>Base of the training center pages: staff only (404 for everyone else).</summary>
public abstract class TcPageModel(CurrentUser me) : PageModel
{
    public CurrentUser Me => me;
    public string? Message { get; protected set; }
    public string? Error { get; protected set; }

    protected virtual bool Allowed => me.IsStaff;

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        if (!Allowed) context.Result = new NotFoundResult();
    }
}
