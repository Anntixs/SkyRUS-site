using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Security;
using SkyRus.Site.Services;

namespace SkyRus.Site.Pages.Tc;

public sealed class ProtocolModel(CurrentUser me, ProtocolService protocols, MemberRefresh refresh) : TcPageModel(me)
{
    public Protocol Protocol { get; private set; } = new();
    public string ReportUrl { get; private set; } = "";

    /// <summary>Mentors may open exam protocols but not grade or close them.</summary>
    public bool CanEdit => Protocol.IsOpen && Me.CanRunKind(Protocol.Kind);

    private bool Load(long id)
    {
        if (protocols.Protocol(id) is not { } p) return false;
        Protocol = p;
        ReportUrl = protocols.ReportUrl(p);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!Load(id)) return NotFound();
        if (Protocol.RrStatus == "pending" && await refresh.RefreshAsync(Protocol.Cid, HttpContext.RequestAborted)) Load(id);
        return Page();
    }

    private Dictionary<long, int?> Grades() =>
        Protocol.Items.ToDictionary(i => i.Id, i => int.TryParse(Request.Form[$"g{i.Id}"], out var g) ? g : (int?)null);

    public IActionResult OnPostSave(long id, string? comment)
    {
        if (!Load(id)) return NotFound();
        if (!CanEdit) return NotFound();
        Error = protocols.Save(id, Grades(), comment ?? "");
        if (Error == null) Message = "Сохранено";
        Load(id);
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(long id, string? comment)
    {
        if (!Load(id)) return NotFound();
        if (!CanEdit) return NotFound();
        Error = protocols.Save(id, Grades(), comment ?? "") ?? await protocols.CloseAsync(Me.Cid, id, HttpContext.RequestAborted);
        Load(id);
        if (Error == null)
            Message = Protocol.Status == "passed"
                ? Protocol.IsExam
                    ? Protocol.RrId != null ? $"Экзамен сдан. Заявка на рейтинг {Protocol.TargetRating} отправлена в SkyNetwork." : "Экзамен сдан. Заявку на рейтинг не удалось отправить сейчас, сайт повторит попытку сам."
                    : "Аттестация сдана"
                : "Аттестация не сдана";
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync(long id)
    {
        if (!Load(id)) return NotFound();
        if (!Me.IsInstructor) return NotFound();
        if (Protocol.RatingRequestUnsent) await protocols.SendRatingRequestAsync(id, HttpContext.RequestAborted);
        else await protocols.RefreshRatingRequestAsync(Protocol, HttpContext.RequestAborted);
        Load(id);
        Message = Protocol.RrId != null ? "Статус заявки обновлён" : null;
        return Page();
    }
}
