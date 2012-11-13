using System.Web.Mvc;

namespace NuGet.Server.Controllers
{
    public class HomeController : Controller
    {
        public ViewResult Index()
        {
            return View("Index");
        }
    }
}