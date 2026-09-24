using Microsoft.AspNetCore.Mvc;
using SkyRus.Site.Data;
using SkyRus.Site.Security;

namespace SkyRus.Site.Pages.Admin;

public sealed class DocumentEditModel(CurrentUser me, SiteContent content) : AdminPageModel(me)
{
    public Document Doc { get; private set; } = new();
    public bool IsNew { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (id == "new") { IsNew = true; return Page(); }
        if (!long.TryParse(id, out var did) || content.Document(did) is not { } d) return NotFound();
        Doc = d;
        return Page();
    }

    public IActionResult OnPost(string id, string? title, string? category, string? body, string? url, int sort)
    {
        long? did = id == "new" ? null : long.TryParse(id, out var v) && content.Document(v) != null ? v : -1;
        if (did == -1) return NotFound();
        url = (url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title)) Error = "Укажите название";
        else if (url.Length > 0 && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
            Error = "Ссылка должна начинаться с http:// или https://";
        else if (string.IsNullOrWhiteSpace(body) && url.Length == 0) Error = "Добавьте текст или ссылку на файл";
        if (Error != null)
        {
            IsNew = did == null;
            Doc = new Document { Title = title ?? "", Category = category ?? "", Body = body ?? "", Url = url, Sort = sort };
            return Page();
        }
        long saved = content.SaveDocument(did, title!.Trim(), (category ?? "").Trim(), (body ?? "").Trim(), url, sort);
        return Redirect($"/documents/{saved}");
    }

    public IActionResult OnPostDelete(long id)
    {
        content.DeleteDocument(id);
        return Redirect("/admin/documents");
    }
}
