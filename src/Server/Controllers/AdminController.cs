using System;
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
            Indexer.BeginSynchronizeIndexWithFileSystem(EndSynchronize, null);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult Optimize()
        {
            Action call = Indexer.Optimize;

            call.BeginInvoke(call.EndInvoke, null);

            return RedirectToAction("Index", "Home");
        }

        private void EndSynchronize(IAsyncResult ar)
        {
            try
            {
                Indexer.EndSynchronizeIndexWithFileSystem(ar);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}