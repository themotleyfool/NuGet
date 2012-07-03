using System.Linq;
using System.Web.Mvc;
using Ninject;
using NuGet.Server.Infrastructure.Lucene;

namespace NuGet.Server.Controllers
{
    public class TabCompletionController : Controller
    {
        [Inject]
        public ILucenePackageRepository Repository { get; set; }

        public JsonResult GetMatchingPackages(string partialId, bool? includePrerelease, int maxResults)
        {
            var packages = GetPackages(includePrerelease);

            if (!string.IsNullOrWhiteSpace(partialId))
            {
                packages = packages.Where(p => p.Id.StartsWith(partialId));
            }

            packages = packages.OrderBy(p => p.Id);

            /* Note: Lucene.Net.Linq does not support Distinct(), so we take up to 10 times maxResults
             * (possibly non-unique) results before removing duplicates. This means that less than 30
             * results could be returned when there are many versions of a matching package.
             * 
             * This strategy prevents empty partialId queries on very large repositories from being slow.
             */
            var data = packages.Select(p => p.Id).Take(maxResults * 10).ToArray().Distinct().Take(maxResults);

            return JsonResult(data);
        }

        public JsonResult GetPackageVersions(string packageId, bool? includePrerelease)
        {
            var packages = GetPackages(includePrerelease).Where(p => p.Id == packageId);

            var data = packages.OrderBy(p => p.Version).Select(p => p.Version.ToString()).ToArray();

            return JsonResult(data);
        }

        private IQueryable<LucenePackage> GetPackages(bool? includePrerelease)
        {
            if (!includePrerelease.GetValueOrDefault(false))
            {
                return Repository.LucenePackages.Where(p => !p.IsPrerelease);
            }

            return Repository.LucenePackages;
        }

        private static JsonResult JsonResult(object data)
        {
            return new JsonResult {Data = data, JsonRequestBehavior = JsonRequestBehavior.AllowGet};
        }
    }
}