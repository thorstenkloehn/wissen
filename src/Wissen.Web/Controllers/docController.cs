using Microsoft.AspNetCore.Mvc;

namespace Wissen.Web.Controllers
{
    [Route("doc")]
    public class docController : Controller
    {
        // GET: docController
        public ActionResult Index()
        {
            return View();
        }

    }
}
