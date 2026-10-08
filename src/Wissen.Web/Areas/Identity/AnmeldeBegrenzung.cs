using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace Wissen.Web.Areas.Identity;

// Begrenzt Anmeldeversuche je IP-Adresse. Die Kontosperre allein lässt sich missbrauchen: Wer die
// E-Mail-Adresse kennt, hält das Konto mit falschen Passwörtern dauerhaft gesperrt. Deshalb bremst
// zuerst diese Grenze den einzelnen Absender, und das Konto wird erst nach deutlich mehr
// Fehlversuchen gesperrt (Lockout.MaxFailedAccessAttempts in Program.cs).
public static class AnmeldeBegrenzung
{
    public const int Versuche = 5;

    public static readonly TimeSpan Zeitraum = TimeSpan.FromMinutes(5);

    public const string Meldung = "Zu viele Anmeldeversuche. Bitte versuchen Sie es in einigen Minuten erneut.";

    public static PartitionedRateLimiter<HttpContext> CreateLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            Partition(context) is { } partition
                ? RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Versuche,
                    Window = Zeitraum,
                })
                : RateLimitPartition.GetNoLimiter(""));

    // Schlüssel, unter dem die Versuche gezählt werden, oder null, wenn die Anfrage keine Anmeldung ist.
    // Gezählt werden Login, LoginWith2fa und LoginWithRecoveryCode.
    public static string? Partition(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method)
            || request.Path.Value?.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        return Absender(context.Connection.RemoteIpAddress);
    }

    // Bei IPv6 gehört meist ein ganzes /64-Netz demselben Anschluss; einzelne Adressen daraus
    // zu zählen, wäre wirkungslos.
    public static string Absender(IPAddress? address)
    {
        if (address is null)
        {
            return "unbekannt";
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? Convert.ToHexString(address.GetAddressBytes(), 0, 8)
            : address.ToString();
    }
}
