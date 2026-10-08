using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;

namespace Wissen.Web.Controllers
{
    [Route("{*path}")]
    public class docController(ApplicationDbContext db) : Controller
    {
        // GET: docController
        public async Task<IActionResult> HandleAll(string? path)
        {
            var seite = await db.Seiten
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Path == path);

            if (seite is not null)
            {
                // Für die Liste der Versionen genügen die Kopfdaten, nicht die Inhalte.
                seite.Versionen = await db.SeitenVersionen
                    .AsNoTracking()
                    .Where(v => v.SeiteId == seite.Id)
                    .OrderByDescending(v => v.Nummer)
                    .Select(v => new SeitenVersion { Nummer = v.Nummer, Kategorie = v.Kategorie, ErstelltAm = v.ErstelltAm })
                    .ToListAsync();
            }

            ViewData["Path"] = path;
            return View("Index", seite);
        }

    }
}
