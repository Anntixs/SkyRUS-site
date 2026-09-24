using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class TemplateModel(CurrentUser me, ProtocolService protocols) : AdminPageModel(me)
{
    public Template Template { get; private set; } = new() { Active = true };
    public string ItemsText { get; private set; } = "";
    public bool IsNew { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (id == "new") { IsNew = true; return Page(); }
        if (!long.TryParse(id, out var tid) || protocols.Template(tid) is not { } t) return NotFound();
        Template = t;
        ItemsText = string.Join("\n", t.Items.Select(i => i.Description.Length > 0 ? $"{i.Title} | {i.Description}" : i.Title));
        return Page();
    }

    public IActionResult OnPost(string id, string? title, string? kind, string? track, string? targetRating, int sort, string? active, string? items)
    {
        long? tid = id == "new" ? null : long.TryParse(id, out var v) && protocols.Template(v) != null ? v : -1;
        if (tid == -1) return NotFound();
        var (saved, error) = protocols.SaveTemplate(tid, title ?? "", kind ?? "", track ?? "atc", targetRating ?? "", sort, active == "true", items ?? "");
        if (error == null) return Redirect("/admin/templates");
        Error = error;
        IsNew = tid == null;
        Template = new Template { Id = saved, Title = title ?? "", Kind = kind ?? "theory", Track = track ?? "atc", TargetRating = targetRating ?? "", Sort = sort, Active = active == "true" };
        ItemsText = items ?? "";
        return Page();
    }
}
