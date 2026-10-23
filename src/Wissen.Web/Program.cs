using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wissen.Web;
using Wissen.Web.Areas.Identity;
using Wissen.Infrastructure;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        // Den einzelnen Absender bremst schon AnmeldeBegrenzung nach 5 Versuchen. Die Sperre des Kontos
        // greift erst später, damit nicht jeder ein fremdes Konto mit wenigen Versuchen sperren kann.
        options.Lockout.MaxFailedAccessAttempts = 20;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<GermanIdentityErrorDescriber>();
// Außerhalb der Entwicklung (dort gibt es das Profil "http") geht das Anmelde-Cookie nur über HTTPS.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
    builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
}
// Ein Jahr statt der Voreinstellung von 30 Tagen: Der Browser fragt die Domain so lange nur per HTTPS an.
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
builder.Services.AddControllersWithViews();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        [AnmeldeBegrenzung.CreateLimiter(), PasswortBegrenzung.CreateLimiter(), .. SpeicherBegrenzung.CreateLimiters()]);
    options.OnRejected = async (context, cancellationToken) =>
    {
        var (zeitraum, meldung) =
            SpeicherBegrenzung.Partition(context.HttpContext) is not null ? (SpeicherBegrenzung.Zeitraum, SpeicherBegrenzung.Meldung)
            : PasswortBegrenzung.Partition(context.HttpContext) is not null ? (PasswortBegrenzung.Zeitraum, PasswortBegrenzung.Meldung)
            : (AnmeldeBegrenzung.Zeitraum, AnmeldeBegrenzung.Meldung);
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = ((int)zeitraum.TotalSeconds).ToString();
        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync(meldung, cancellationToken);
    };
});

// Seiten, die mit 404 antworten. Ohne E-Mail-Versand und externe Anmeldedienste bewirken diese
// Seiten nichts; ein neues Passwort setzt die Konsolenanwendung (konto-passwort).
List<string> gesperrteSeiten =
[
    "/Account/ForgotPassword",
    "/Account/ForgotPasswordConfirmation",
    "/Account/ResetPassword",
    "/Account/ResetPasswordConfirmation",
    "/Account/ResendEmailConfirmation",
    "/Account/ConfirmEmail",
    "/Account/ConfirmEmailChange",
    "/Account/ExternalLogin",
    "/Account/Manage/Email",
    "/Account/Manage/ExternalLogins",
];
// Ohne "RegistrierungErlaubt": true kann sich niemand selbst ein Konto anlegen.
if (!builder.Configuration.GetValue<bool>("RegistrierungErlaubt"))
{
    gesperrteSeiten.AddRange(["/Account/Register", "/Account/RegisterConfirmation"]);
}
builder.Services.AddRazorPages(options =>
{
    foreach (var page in gesperrteSeiten)
    {
        options.Conventions.AddAreaPageApplicationModelConvention("Identity", page,
            model => model.Filters.Add(new SeiteGesperrtFilter()));
    }
});

var app = builder.Build();

// Ausstehende Migrationen bei jedem Start anwenden, damit die Datenbank nach einem Neustart zur
// eingespielten Version passt. Scheitert das, startet die App nicht (systemd versucht es erneut).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("{Anzahl} Migration(en) angewendet: {Migrationen}", pending.Count, string.Join(", ", pending));
    }
}

// Sicherheits-Header für jede Antwort. Die Content-Security-Policy erlaubt Skripte und Stile nur aus
// eigenen Dateien: Inline-Skripte, onclick-, style-Attribute und <style> in Views blockiert der Browser.
// Stile, die Skripte über element.style setzen (Bootstrap), sind davon nicht betroffen.
var contentSecurityPolicy = string.Join("; ",
    "default-src 'self'",
    // Keine Bilder von fremden Servern: Sie verrieten dem fremden Server jeden Besucher der Seite.
    "img-src 'self' data:",
    "style-src 'self'",
    // Hot Reload (dotnet watch) spricht in der Entwicklung über WebSockets mit dem Browser.
    app.Environment.IsDevelopment() ? "connect-src 'self' ws://localhost:* wss://localhost:*" : "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'self'");
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = contentSecurityPolicy;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Die App braucht weder Kamera, Mikrofon noch Standort; auch eingebettete Inhalte bekommen sie nicht.
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    // Ein von hier geöffnetes fremdes Fenster bekommt keinen Zugriff auf dieses (window.opener).
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // Dauer siehe AddHsts oben.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Vor UseRateLimiter: SpeicherBegrenzung und PasswortBegrenzung zählen je Konto und brauchen dafür den angemeldeten Benutzer.
app.UseAuthentication();
app.UseRateLimiter();

app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/", () => Results.Redirect("/doc"));

app.MapControllers()
   .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
