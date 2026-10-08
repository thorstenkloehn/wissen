using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;

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

            ViewData["Path"] = path;
            return View("Index", seite);
        }

    }
}
