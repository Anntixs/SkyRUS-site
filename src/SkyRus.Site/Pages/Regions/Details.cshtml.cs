using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;

namespace SkyRus.Site.Pages.Regions;

public sealed class DetailsModel(SiteContent content, TrainingService training) : PageModel
{
    public Fir Fir { get; private set; } = new();
    public IReadOnlyList<Permit> Permits { get; private set; } = [];

    public IActionResult OnGet(string code)
    {
        var firs = content.Firs();
        if (firs.FirstOrDefault(f => f.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) is not { } fir) return NotFound();
        Fir = fir;
        Permits = training.Permits().Where(p => SiteContent.FirOfPosition(firs, p.Position)?.Id == fir.Id).ToList();
        return Page();
    }
}
