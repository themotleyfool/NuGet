using System;
using System.Collections.Generic;
using NuGet.Server.Models;

namespace NuGet.Server.ViewModels
{
    public class SearchResultsViewModel : ISearchForm
    {
        private const string _defaultIconUri = "http://nuget.org/Content/Images/packageDefaultIcon-50x50.png";

        public string Query { get; set; }
        public bool IncludePrerelease { get; set; }
        public IEnumerable<IPackage> Hits { get; set; }
        public int TotalHits { get; set; }
        public int First { get; set; }
        public int Last { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool IsLastPage { get; set; }

        public string DefaultIconUrl
        {
            get { return _defaultIconUri; }
        }

        public SearchResultsViewModel(SearchForm form)
        {
            Query = form.Query;
            IncludePrerelease = form.IncludePrerelease;
            Page = form.Page;
            PageSize = form.PageSize;
        }
    }
}