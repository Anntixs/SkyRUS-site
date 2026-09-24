using SkyRus.Site.Pages.Tc;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

/// <summary>Administration pages: division administrators only (404 for everyone else).</summary>
public abstract class AdminPageModel(CurrentUser me) : TcPageModel(me)
{
    protected override bool Allowed => Me.IsAdmin;
}
