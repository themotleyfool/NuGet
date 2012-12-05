using System;
using System.Threading.Tasks;
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
            Indexer.SynchronizeIndexWithFileSystem().ContinueWith(LogException);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult Optimize()
        {
            Task.Run((Action) Indexer.Optimize).ContinueWith(LogException);

            return RedirectToAction("Index", "Home");
        }

        private void LogException(Task task)
        {
            if (task.Exception != null)
            {
                Log.Error(task.Exception);
            }
        }
    }
}