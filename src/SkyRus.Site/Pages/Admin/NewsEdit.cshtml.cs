using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class NewsEditModel(CurrentUser me, SiteContent content) : AdminPageModel(me)
{
    public NewsPost Post { get; private set; } = new() { Published = true };
    public bool IsNew { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (id == "new") { IsNew = true; return Page(); }
        if (!long.TryParse(id, out var nid) || content.Post(nid) is not { } p) return NotFound();
        Post = p;
        return Page();
    }

    public IActionResult OnPost(string id, string? title, string? body, string? published)
    {
        long? nid = id == "new" ? null : long.TryParse(id, out var v) && content.Post(v) != null ? v : -1;
        if (nid == -1) return NotFound();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            Error = "Заполните заголовок и текст";
            IsNew = nid == null;
            Post = new NewsPost { Title = title ?? "", Body = body ?? "", Published = published == "true" };
            return Page();
        }
        long saved = content.SavePost(nid, Me.Cid, title.Trim(), body.Trim(), published == "true");
        return Redirect($"/news/{saved}");
    }

    public IActionResult OnPostDelete(long id)
    {
        content.DeletePost(id);
        return Redirect("/admin/news");
    }
}
