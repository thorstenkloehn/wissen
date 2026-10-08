using System.Security.Claims;
using System.Threading.RateLimiting;
using Wissen.Web.Areas.Identity;

namespace Wissen.Web;

// Begrenzt das Speichern von Seiten je Konto. Markdown in HTML umzusetzen kann bei ungünstigen
// Eingaben viele Sekunden Rechenzeit kosten, und jede Änderung legt eine weitere Version in der
// Datenbank ab. Ohne Grenze könnte ein einzelnes Konto den Server auslasten und die Datenbank füllen.
public static class SpeicherBegrenzung
{
    public const int Anfragen = 20;

    public static readonly TimeSpan Zeitraum = TimeSpan.FromMinutes(5);

    public const string Meldung = "Zu viele Änderungen in kurzer Zeit. Bitte versuchen Sie es in einigen Minuten erneut.";

    // Zuerst die Grenze für gleichzeitige Anfragen: Eine dort abgewiesene Anfrage zählt nicht im Zeitraum.
    public static PartitionedRateLimiter<HttpContext>[] CreateLimiters() =>
    [
        // Je Konto wird nur eine Änderung zur selben Zeit bearbeitet; so belegt ein Konto höchstens einen Prozessorkern.
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Partition(context) is { } partition
                ? RateLimitPartition.GetConcurrencyLimiter(partition, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 0,
                })
                : RateLimitPartition.GetNoLimiter("")),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Partition(context) is { } partition
                ? RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Anfragen,
                    Window = Zeitraum,
                })
                : RateLimitPartition.GetNoLimiter("")),
    ];

    // Schlüssel, unter dem die Änderungen gezählt werden, oder null, wenn die Anfrage keine Seite ändert.
    // Gezählt werden alle POST-Anfragen an den SeiteController (anlegen, bearbeiten, zurücksetzen, löschen).
    public static string? Partition(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method) || !request.Path.StartsWithSegments("/seite", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Ohne Anmeldung weist die App die Anfrage ohnehin ab; gezählt wird sie dann je Absender.
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } konto
            ? $"konto:{konto}"
            : $"absender:{AnmeldeBegrenzung.Absender(context.Connection.RemoteIpAddress)}";
    }
}
