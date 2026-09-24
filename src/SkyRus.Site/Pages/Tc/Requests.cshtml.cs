using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

public sealed class RequestsModel(CurrentUser me, SiteContent content, TrainingService training) : TcPageModel(me)
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public long? FirId { get; private set; }
    public bool Archive { get; private set; }
    public IReadOnlyList<TrainingRequest> List { get; private set; } = [];

    public void OnGet(long? fir, int? archive)
    {
        Firs = content.Firs();
        FirId = Firs.Any(f => f.Id == fir) ? fir : null;
        Archive = archive == 1;
        List = training.Requests(Archive, FirId);
    }
}
