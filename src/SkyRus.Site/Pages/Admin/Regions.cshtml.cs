using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class RegionsModel(CurrentUser me, SiteContent content) : AdminPageModel(me)
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];

    public void OnGet() => Firs = content.Firs();

    public void OnPostFir(long? id, string? code, string? name, string? description)
    {
        Error = content.SaveFir(id is > 0 ? id : null, code ?? "", name ?? "", description ?? "");
        if (Error == null) Message = "РПИ сохранён";
        OnGet();
    }

    public void OnPostAirport(long firId, string? icao, string? name)
    {
        Error = content.AddAirport(firId, icao ?? "", name ?? "");
        if (Error == null) Message = "Аэродром сохранён";
        OnGet();
    }

    public void OnPostRemoveAirport(string icao)
    {
        content.RemoveAirport(icao);
        Message = "Аэродром удалён";
        OnGet();
    }
}
