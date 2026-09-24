using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

public sealed class StudentModel(CurrentUser me, SiteContent content, TrainingService training, ProtocolService protocols,
    UserService users, INetworkClient network) : TcPageModel(me)
{
    public Student Student { get; private set; } = new();
    public User? Member { get; private set; }
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public IReadOnlyList<Permit> Permits { get; private set; } = [];
    public IReadOnlyList<Protocol> Protocols { get; private set; } = [];
    public IReadOnlyList<Template> Templates { get; private set; } = [];
    public IReadOnlyList<Comment> Log { get; private set; } = [];
    public IReadOnlyList<TrainingRequest> Requests { get; private set; } = [];

    private bool Load(long cid)
    {
        if (training.Student(cid) is not { } s) return false;
        Student = s;
        Member = users.Find(cid);
        Firs = content.Firs();
        Permits = training.Permits(cid, activeOnly: false);
        Protocols = protocols.Protocols(cid);
        Templates = protocols.Templates().Where(t => Me.CanRunKind(t.Kind)).ToList();
        Log = training.Log(cid);
        Requests = training.Requests(cid: cid);
        return true;
    }

    public IActionResult OnGet(long cid) => Load(cid) ? Page() : NotFound();

    public IActionResult OnPostHome(long cid, long firId, string? airport)
    {
        if (!Load(cid)) return NotFound();
        Error = training.SetHome(Me.Cid, cid, firId, airport ?? "");
        if (Error == null) Message = "РПИ приписки изменён";
        Load(cid);
        return Page();
    }

    public IActionResult OnPostPermit(long cid, string? position, string? note)
    {
        if (!Load(cid)) return NotFound();
        if (!Me.IsInstructor) return NotFound();
        Error = training.IssuePermit(Me.Cid, cid, position ?? "", note ?? "");
        if (Error == null) Message = "Допуск выдан";
        Load(cid);
        return Page();
    }

    public IActionResult OnPostRevoke(long cid, long permitId)
    {
        if (!Load(cid)) return NotFound();
        if (!Me.IsInstructor) return NotFound();
        if (Permits.Any(p => p.Id == permitId)) training.RevokePermit(Me.Cid, permitId);
        Message = "Допуск отозван";
        Load(cid);
        return Page();
    }

    public IActionResult OnPostComment(long cid, string? body)
    {
        if (!Load(cid)) return NotFound();
        if (string.IsNullOrWhiteSpace(body)) Error = "Напишите комментарий";
        else
        {
            training.Log(cid, Me.Cid, body);
            Message = "Комментарий добавлен";
        }
        Load(cid);
        return Page();
    }

    public IActionResult OnPostProtocol(long cid, long templateId)
    {
        if (!Load(cid)) return NotFound();
        if (Templates.All(t => t.Id != templateId))
        {
            Error = "Выберите вид аттестации";
            return Page();
        }
        var (id, error) = protocols.Create(Me.Cid, cid, templateId);
        if (error != null)
        {
            Error = error;
            return Page();
        }
        return Redirect($"/tc/protocols/{id}");
    }

    /// <summary>Fresh name and rating from the network.</summary>
    public async Task<IActionResult> OnPostRefreshAsync(long cid)
    {
        if (!Load(cid)) return NotFound();
        if (await network.MemberAsync(cid, HttpContext.RequestAborted) is { } m)
        {
            users.Upsert(m);
            Message = "Данные обновлены из SkyNetwork";
        }
        else Error = "SkyNetwork недоступен";
        Load(cid);
        return Page();
    }
}
