using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

[Route("Error")]
public class ErrorController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
