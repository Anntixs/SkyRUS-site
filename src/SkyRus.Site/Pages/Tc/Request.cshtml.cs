using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Tc;

public sealed class RequestModel(CurrentUser me, TrainingService training) : TcPageModel(me)
{
    public TrainingRequest Item { get; private set; } = new();

    private bool Load(long id)
    {
        if (training.RequestById(id) is not { } r) return false;
        Item = r;
        return true;
    }

    public IActionResult OnGet(long id) => Load(id) ? Page() : NotFound();

    public IActionResult OnPostAccept(long id)
    {
        if (!Load(id)) return NotFound();
        if (!training.Accept(Me.Cid, id)) Error = "Заявка уже рассмотрена";
        else return Redirect($"/tc/students/{Item.Cid}");
        return Page();
    }

    public IActionResult OnPostDecline(long id, string? reason)
    {
        if (!Load(id)) return NotFound();
        Error = training.Decline(Me.Cid, id, reason ?? "");
        if (Error == null) Message = "Заявка отклонена, студент увидит причину в личном кабинете";
        Load(id);
        return Page();
    }
}
