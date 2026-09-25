using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;

namespace SkyRus.Site.Pages;

public sealed class PermitsModel(SiteContent content, TrainingService training) : PageModel
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public Fir? Selected { get; private set; }
    /// <summary>Each controller once: the name and every position they hold a permit for.</summary>
    public IReadOnlyList<(string Name, IReadOnlyList<string> Positions)> List { get; private set; } = [];

    public void OnGet(string? fir)
    {
        Firs = content.Firs();
        Selected = Firs.FirstOrDefault(f => f.Code.Equals(fir, StringComparison.OrdinalIgnoreCase));
        List = training.Permits()
            .Where(p => Selected == null || SiteContent.FirOfPosition(Firs, p.Position)?.Id == Selected.Id)
            .GroupBy(p => p.Cid)
            .Select(g => (Name: g.First().Name.Length > 0 ? g.First().Name : g.Key.ToString(),
                          Positions: (IReadOnlyList<string>)g.Select(p => p.Position).Distinct().Order(StringComparer.Ordinal).ToList()))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
