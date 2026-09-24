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
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    /// <summary>Why a new request cannot be sent (already sent, or already a student), or null.</summary>
    public string? RequestBlocked { get; private set; }

    public void OnGet()
    {
        Firs = content.Firs();
        Student = training.Student(me.Cid);
        Requests = training.Requests(cid: me.Cid);
        Permits = training.Permits(me.Cid);
        Exams = protocols.RatingRequestsOf(me.Cid);
        RequestBlocked = training.CanRequest(me.Cid);
    }

    // Redirect after the post, so reloading the page does not send the form again.
    public IActionResult OnPost(long firId, string? airport, string? message)
    {
        Error = training.Request(me.Cid, firId, airport ?? "", message ?? "");
        if (Error == null) Message = "Заявка отправлена";
        return RedirectToPage();
    }
}
