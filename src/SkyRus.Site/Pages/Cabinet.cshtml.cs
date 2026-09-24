using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages;

public sealed class CabinetModel(CurrentUser me, SiteContent content, TrainingService training, ProtocolService protocols) : PageModel
{
    public IReadOnlyList<Fir> Firs { get; private set; } = [];
    public Student? Student { get; private set; }
    public IReadOnlyList<TrainingRequest> Requests { get; private set; } = [];
    public IReadOnlyList<Permit> Permits { get; private set; } = [];
    public IReadOnlyList<Protocol> Exams { get; private set; } = [];
    public string? Message { get; private set; }
    public string? Error { get; private set; }

    public bool HasOpenRequest => Requests.Any(r => r.Status == "open");

    public void OnGet()
    {
        Firs = content.Firs();
        Student = training.Student(me.Cid);
        Requests = training.Requests(cid: me.Cid);
        Permits = training.Permits(me.Cid);
        Exams = protocols.RatingRequestsOf(me.Cid);
    }

    public IActionResult OnPost(long firId, string? airport, string? message)
    {
        Error = training.Request(me.Cid, firId, airport ?? "", message ?? "");
        if (Error == null) Message = "Заявка отправлена. Инструктор или ментор РПИ свяжется с вами.";
        OnGet();
        return Page();
    }
}
