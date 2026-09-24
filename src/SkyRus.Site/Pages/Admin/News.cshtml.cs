using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class NewsModel(CurrentUser me, SiteContent content) : AdminPageModel(me)
{
    public IReadOnlyList<NewsPost> List { get; private set; } = [];
    public void OnGet() => List = content.News(200, all: true);
}
