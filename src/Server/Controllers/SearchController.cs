using System;
using System.Linq;
using System.Web.Mvc;
using Ninject;
using NuGet.Server.Infrastructure;
using NuGet.Server.Models;
using NuGet.Server.ViewModels;

namespace NuGet.Server.Controllers
{
    public class SearchController : Controller
    {
        [Inject]
        public IServerPackageRepository Repository { get; set; }

        public ViewResult Search(SearchForm form)
        {
            var queryable = Repository.Search(form.Query, new string[0], form.IncludePrerelease).Where(p => p.IsLatestVersion);
            var totalHits = queryable.Count();
            var first = form.Page*form.PageSize;
            var hits = queryable.Skip(first).Take(form.PageSize).ToList();

            return View(new SearchResultsViewModel(form)
                            {
                                TotalHits =  totalHits,
                                Hits = hits,
                                First = first + 1,
                                Last = Math.Min(first + form.PageSize, totalHits),
                                IsLastPage = (form.Page + 1) * form.PageSize >= totalHits
                            });
        }
    }
}