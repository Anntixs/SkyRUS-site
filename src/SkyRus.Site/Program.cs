using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.WebEncoders;
using SkyRus.Site;
using SkyRus.Site.Data;
using SkyRus.Site.Network;
using SkyRus.Site.Security;
using SkyRus.Site.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<SiteContent>();
builder.Services.AddSingleton<TrainingService>();
builder.Services.AddSingleton<ProtocolService>();
builder.Services.AddHttpClient("network", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<INetworkClient, HttpNetworkClient>();
builder.Services.AddSingleton<RatingRequestSync>();
builder.Services.AddSingleton<MemberRefresh>();
if (builder.Configuration.GetValue("Site:BackgroundSync", true))
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RatingRequestSync>());
builder.Services.AddScoped<CurrentUser>();
// Sign-in cookies survive restarts: the keys live next to the database.
var dbPath = builder.Configuration["Site:Database"] ?? "skyrus.db";
builder.Services.AddDataProtection().SetApplicationName("SkyRUS")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "keys")));

builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.AccessDeniedPath = "/login";
        o.Cookie.Name = "skyrus";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizePage("/Cabinet");
    o.Conventions.AuthorizeFolder("/Tc");
    o.Conventions.AuthorizeFolder("/Admin");
}).AddMvcOptions(o => o.ModelMetadataDetailsProviders.Add(new KeepEmptyStrings()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    int limit = builder.Configuration.GetValue("Site:LoginAttemptsPerMinute", 30);
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
app.Services.GetRequiredService<Database>().Migrate();

if (app.Configuration.GetValue<bool>("Site:BehindProxy"))
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    forwarded.KnownNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error/500");
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    var user = ctx.RequestServices.GetRequiredService<CurrentUser>();
    await user.LoadAsync(ctx);
    // The rating and rating requests follow the network without signing in again.
    if (user.SignedIn && HttpMethods.IsGet(ctx.Request.Method) && !ctx.Request.Path.StartsWithSegments("/login")
        && await ctx.RequestServices.GetRequiredService<MemberRefresh>().RefreshAsync(user.Cid, ctx.RequestAborted))
        await user.LoadAsync(ctx);
    // The training center and administration do not exist for anyone else: plain 404, not indexed.
    bool tc = ctx.Request.Path.StartsWithSegments("/tc"), admin = ctx.Request.Path.StartsWithSegments("/admin");
    if (tc || admin)
    {
        if (user.SignedIn && (admin ? !user.IsAdmin : !user.IsStaff))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        ctx.Response.Headers.CacheControl = "no-store";
    }
    await next();
});
app.UseAuthorization();
app.MapRazorPages();
// News banners: drafts only for the administrators who edit them.
app.MapGet("/news/{id:long}/banner", (long id, HttpContext ctx, SiteContent content, CurrentUser user) =>
{
    if (content.Banner(id) is not { } b || (!b.Published && !user.IsAdmin)) return Results.NotFound();
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers.CacheControl = b.Published ? "public, max-age=604800" : "private, no-store";
    return Results.File(b.Data, b.Type);
});
app.Run();

/// <summary>Form fields left empty bind as "" rather than null.</summary>
sealed class KeepEmptyStrings : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        if (context.Key.ModelType == typeof(string)) context.DisplayMetadata.ConvertEmptyStringToNull = false;
    }
}

/// <summary>Entry point, visible to integration tests.</summary>
public partial class Program;
