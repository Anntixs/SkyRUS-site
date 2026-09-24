using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

public sealed class StudentsModel(CurrentUser me, SiteContent content, TrainingService training, UserService users, INetworkClient network) : TcPageModel(me)
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public long? FirId { get; private set; }
    public string Query { get; private set; } = "";
    public int Unassigned { get; private set; }
    public IReadOnlyList<Student> List { get; private set; } = [];

    public void OnGet(long? fir, string? q)
    {
        Firs = content.Firs();
        FirId = Firs.Any(f => f.Id == fir) ? fir : null;
        Query = (q ?? "").Trim();
        var all = training.Students(query: Query);
        Unassigned = all.Count(s => s.FirId == null);
        List = FirId == null ? all : all.Where(s => s.FirId == FirId).ToList();
    }

    /// <summary>Opens a card for any network member by CID (e.g. a controller who came from elsewhere).</summary>
    public async Task<IActionResult> OnPostOpenAsync(long cid)
    {
        if (users.Find(cid) == null)
        {
            if (await network.MemberAsync(cid, HttpContext.RequestAborted) is not { } m)
            {
                Error = "Участник с таким CID в SkyNetwork не найден";
                OnGet(null, null);
                return Page();
            }
            users.Upsert(m);
        }
        training.EnsureStudent(Me.Cid, cid);
        return Redirect($"/tc/students/{cid}");
    }
}
