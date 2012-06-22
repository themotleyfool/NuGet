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
            Indexer.BeginSynchronizeIndexWithFileSystem(EndSynchrnonize, null);

            return Redirect("~/");
        }

        private void EndSynchrnonize(IAsyncResult ar)
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