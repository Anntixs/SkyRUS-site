using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SkyRus.Site.Data;

namespace SkyRus.Site.Security;

/// <summary>
/// Who is looking at the page. Roles in the training center: mentors work with students and hold
/// theory and practice tests; instructors also hold rating exams and issue permits; administrators
/// additionally manage staff, news, documents, regions and assessment templates.
/// </summary>
public sealed class CurrentUser(UserService users)
{
    public User? User { get; private set; }
    public string? Role { get; private set; }

    public long Cid => User?.Cid ?? 0;
    public bool SignedIn => User != null;
    public bool IsStaff => Role != null;
    public bool IsInstructor => Role is "instructor" or "admin";
    public bool IsAdmin => Role == "admin";
    public string RoleTitle => Role != null ? UserService.Roles[Role] : "";

    /// <summary>Exams (they end with a rating request) only by instructors.</summary>
    public bool CanRunKind(string kind) => IsStaff && (kind != "exam" || IsInstructor);

    public Task LoadAsync(HttpContext ctx)
    {
        if (ctx.User.FindFirstValue("cid") is { } s && long.TryParse(s, out var cid) && users.Find(cid) is { } u)
        {
            User = u;
            Role = users.RoleOf(cid);
        }
        return Task.CompletedTask;
    }

    public static Task SignInAsync(HttpContext ctx, long cid) =>
        ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("cid", cid.ToString())], CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = true });
}
