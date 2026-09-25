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

    public async Task<IActionResult> OnPostAsync(string id, string? title, string? body, string? published, IFormFile? banner, string? removeBanner)
    {
        long? nid = id == "new" ? null : long.TryParse(id, out var v) && content.Post(v) != null ? v : -1;
        if (nid == -1) return NotFound();
        byte[]? picture = null;
        if (banner is { Length: > 0 })
        {
            if (banner.Length > SiteContent.MaxBannerBytes) Error = "Баннер больше 5 МБ";
            else
            {
                using var ms = new MemoryStream();
                await banner.CopyToAsync(ms, HttpContext.RequestAborted);
                picture = ms.ToArray();
                if (SiteContent.ImageType(picture) == null) Error = "Баннер должен быть картинкой PNG, JPEG, GIF или WebP";
            }
        }
        if (Error == null && (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))) Error = "Заполните заголовок и текст";
        if (Error != null)
        {
            IsNew = nid == null;
            var old = nid is { } e ? content.Post(e) : null;
            Post = new NewsPost { Id = old?.Id ?? 0, BannerAt = old?.BannerAt, Title = title ?? "", Body = body ?? "", Published = published == "true" };
            return Page();
        }
        long saved = content.SavePost(nid, Me.Cid, title!.Trim(), body!.Trim(), published == "true");
        if (picture != null) content.SetBanner(saved, picture);
        else if (removeBanner == "true") content.RemoveBanner(saved);
        return Redirect($"/news/{saved}");
    }

    public IActionResult OnPostDelete(long id)
    {
        content.DeletePost(id);
        return Redirect("/admin/news");
    }
}
