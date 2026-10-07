using Microsoft.AspNetCore.Mvc;

namespace Wissen.Web.Controllers
{
    [Route("{*path}")]
    public class docController : Controller
    {
        // GET: docController
        public IActionResult HandleAll(string? path)
        {
            ViewData["Path"] = path;
            return View("Index");
        }

    }
}
