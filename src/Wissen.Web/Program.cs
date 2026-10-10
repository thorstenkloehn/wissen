using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
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

// Seiten, die mit 404 antworten. „Passwort vergessen“ und „Bestätigungs-E-Mail erneut senden“ bewirken
// ohne E-Mail-Versand nichts; ein neues Passwort setzt die Konsolenanwendung (konto-passwort).
List<string> gesperrteSeiten = ["/Account/ForgotPassword", "/Account/ForgotPasswordConfirmation", "/Account/ResendEmailConfirmation"];
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

// Sicherheits-Header für jede Antwort. Die Content-Security-Policy erlaubt Skripte nur aus
// eigenen Dateien: Inline-Skripte und onclick-Attribute in Views werden vom Browser blockiert.
var contentSecurityPolicy = string.Join("; ",
    "default-src 'self'",
    // Keine Bilder von fremden Servern: Sie verrieten dem fremden Server jeden Besucher der Seite.
    "img-src 'self' data:",
    "style-src 'self' 'unsafe-inline'",
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
