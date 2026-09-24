using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class DocumentsModel(CurrentUser me, SiteContent content) : AdminPageModel(me)
{
    public IReadOnlyList<Document> List { get; private set; } = [];
    public void OnGet() => List = content.Documents();
}
