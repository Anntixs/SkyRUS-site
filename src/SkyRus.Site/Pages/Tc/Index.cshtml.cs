using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

public sealed class IndexModel(CurrentUser me, SiteContent content, TrainingService training) : TcPageModel(me)
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public IReadOnlyList<Student> Recent { get; private set; } = [];

    public void OnGet()
    {
        Firs = content.Firs();
        Recent = training.Students().Take(10).ToList();
    }
}
