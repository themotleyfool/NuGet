using System.Collections.Generic;
using NuGet.Server.Models;

namespace NuGet.Server.ViewModels
{
    public class SearchResultsViewModel
    {
        public string Query { get; set; }
        public bool IncludePrerelease { get; set; }
        public IEnumerable<IPackage> Hits { get; set; }
        public int TotalHits { get; set; }
        public int First { get; set; }
        public int Last { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool IsLastPage { get; set; }

        public SearchResultsViewModel(SearchForm form)
        {
            Query = form.Query;
            IncludePrerelease = form.IncludePrerelease;
            Page = form.Page;
            PageSize = form.PageSize;
        }
    }
}