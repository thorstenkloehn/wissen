using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Wissen.Web.Areas.Identity;

// Begrenzt je Konto, wie oft ein angemeldetes Konto nach seinem aktuellen Passwort gefragt werden kann.
// „Passwort ändern“ und „Konto löschen“ prüfen das Passwort, zählen aber nicht als Anmeldeversuch:
// Wer eine fremde Sitzung übernommen hat, könnte das Passwort dort sonst unbegrenzt raten.
public static class PasswortBegrenzung
{
    public const int Versuche = 5;

    public static readonly TimeSpan Zeitraum = TimeSpan.FromMinutes(5);

    public const string Meldung = "Zu viele Versuche in kurzer Zeit. Bitte versuchen Sie es in einigen Minuten erneut.";

    private static readonly PathString[] Seiten =
    [
        "/Identity/Account/Manage/ChangePassword",
        "/Identity/Account/Manage/DeletePersonalData",
    ];

    public static PartitionedRateLimiter<HttpContext> CreateLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Partition(context) is { } partition
                ? RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Versuche,
                    Window = Zeitraum,
                })
                : RateLimitPartition.GetNoLimiter(""));

    // Schlüssel, unter dem die Versuche gezählt werden, oder null, wenn die Anfrage kein Passwort prüft.
    public static string? Partition(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method)
            || !Seiten.Any(seite => request.Path.StartsWithSegments(seite, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        // Ohne Anmeldung weist die App die Anfrage ohnehin ab; gezählt wird sie dann je Absender.
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } konto
            ? $"konto:{konto}"
            : $"absender:{AnmeldeBegrenzung.Absender(context.Connection.RemoteIpAddress)}";
    }
}
