using System.Web.Mvc;
using Ninject;
using NuGet.Server.Infrastructure.Lucene;

namespace NuGet.Server.Controllers
{
    public class AdminController : Controller
    {
        [Inject]
        public IPackageIndexer Indexer { get; set; }

        public ActionResult Status()
        {
            return View(Indexer.GetIndexingStatus());
        }

        [HttpPost]
        public ActionResult Synchronize()
        {
            Indexer.BeginSynchronizeIndexWithFileSystem(Indexer.EndSynchronizeIndexWithFileSystem, null);

            return Redirect("~/");
        }
    }
}