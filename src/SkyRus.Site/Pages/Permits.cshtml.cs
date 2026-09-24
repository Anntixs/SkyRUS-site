using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;

namespace SkyRus.Site.Pages;

public sealed class PermitsModel(SiteContent content, TrainingService training) : PageModel
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public Fir? Selected { get; private set; }
    public IReadOnlyList<(Permit Permit, Fir? Fir)> List { get; private set; } = [];

    public void OnGet(string? fir)
    {
        Firs = content.Firs();
        Selected = Firs.FirstOrDefault(f => f.Code.Equals(fir, StringComparison.OrdinalIgnoreCase));
        List = training.Permits().Select(p => (p, SiteContent.FirOfPosition(Firs, p.Position)))
            .Where(x => Selected == null || x.Item2?.Id == Selected.Id).ToList();
    }
}
