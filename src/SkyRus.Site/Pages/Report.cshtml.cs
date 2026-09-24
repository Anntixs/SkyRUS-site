using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkyRus.Site.Data;

namespace SkyRus.Site.Pages;

/// <summary>Read-only exam report for network supervisors: opened by the link sent with the rating request.</summary>
public sealed class ReportModel(ProtocolService protocols) : PageModel
{
    public Protocol Protocol { get; private set; } = new();

    public IActionResult OnGet(long id, string? t)
    {
        if (protocols.Protocol(id) is not { } p || p.IsOpen || t == null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(t), Encoding.ASCII.GetBytes(p.ReportToken)))
            return NotFound();
        Protocol = p;
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return Page();
    }
}
