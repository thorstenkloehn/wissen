using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;

namespace Wissen.Web.Controllers
{
    [Route("{*path}")]
    public class docController(ApplicationDbContext db) : Controller
    {
        // Nur lesende Anfragen: Alles andere an eine sonst nicht belegte Adresse beantwortet die App mit 405.
        [AcceptVerbs("GET", "HEAD")]
        public async Task<IActionResult> HandleAll(string? path)
        {
            // Diese Aktion bekommt auch jede Anfrage von Suchrobotern und Angreifern (/wp-login.php, /.env).
            // Unter einem ungültigen Pfad kann keine Seite liegen; dafür wird die Datenbank nicht gefragt.
            var gueltig = Seite.IsValidPath(path);
            ViewData["Path"] = path;
            ViewData["PfadGueltig"] = gueltig;

            var seite = gueltig
                ? await db.Seiten.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path)
                : null;
            if (seite is null)
            {
                // Die Ansicht bietet Angemeldeten an, die Seite anzulegen; der Status sagt trotzdem, dass sie fehlt.
                var fehlt = View("Index", seite);
                fehlt.StatusCode = StatusCodes.Status404NotFound;
                return fehlt;
            }

            // Die Versionsgeschichte sehen nur angemeldete Benutzer.
            if (User?.Identity?.IsAuthenticated == true)
            {
                // Für die Liste der Versionen genügen die Kopfdaten, nicht die Inhalte.
                seite.Versionen = await db.SeitenVersionen
                    .AsNoTracking()
                    .Where(v => v.SeiteId == seite.Id)
                    .OrderByDescending(v => v.Nummer)
                    .Select(v => new SeitenVersion { Nummer = v.Nummer, Kategorie = v.Kategorie, ErstelltAm = v.ErstelltAm, Autor = v.Autor })
                    .ToListAsync();
            }

            return View("Index", seite);
        }

    }
}
