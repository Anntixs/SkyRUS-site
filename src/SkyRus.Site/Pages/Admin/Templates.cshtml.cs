using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class TemplatesModel(CurrentUser me, ProtocolService protocols) : AdminPageModel(me)
{
    public IReadOnlyList<Template> List { get; private set; } = [];
    public void OnGet() => List = protocols.Templates(activeOnly: false);
}
