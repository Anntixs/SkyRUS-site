using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class StaffModel(CurrentUser me, UserService users, INetworkClient network) : AdminPageModel(me)
{
    public IReadOnlyList<StaffMember> List { get; private set; } = [];

    public void OnGet() => List = users.Staff();

    public async Task OnPostAsync(long cid, string? role)
    {
        if (cid <= 0 || role == null || !UserService.Roles.ContainsKey(role)) Error = "Укажите CID и роль";
        else
        {
            if (users.Find(cid) == null && await network.MemberAsync(cid, HttpContext.RequestAborted) is { } m) users.Upsert(m);
            if (users.Find(cid) == null) Error = "Участник с таким CID в SkyNetwork не найден";
            else
            {
                users.SetRole(Me.Cid, cid, role);
                Message = "Сотрудник сохранён";
            }
        }
        OnGet();
    }

    public void OnPostRemove(long cid)
    {
        if (cid == Me.Cid) Error = "Нельзя снять роль с самого себя";
        else
        {
            users.SetRole(Me.Cid, cid, null);
            Message = "Роль снята";
        }
        OnGet();
    }
}
