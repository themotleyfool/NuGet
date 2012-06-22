using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Moq;
using NuGet;
using NuGet.Server.Controllers;
using NuGet.Server.Infrastructure;
using NuGet.Server.Infrastructure.Lucene;
using NuGet.Server.Models;
using NuGet.Server.ViewModels;
using Xunit;

namespace Server.Test.Controllers
{
    public class SearchControllerTests
    {
        private readonly SearchController controller;
        private readonly Mock<IServerPackageRepository> repository;

        public SearchControllerTests()
        {
            repository = new Mock<IServerPackageRepository>();
            controller = new SearchController { Repository = repository.Object };
        }

        [Fact]
        public void ReturnsSearchViewModel()
        {
            var searchForm = new SearchForm();
            SetupRepositorySearch(searchForm, 10);
            
            var result = controller.Search(searchForm);

            Assert.IsType<SearchResultsViewModel>(result.Model);

            repository.VerifyAll();
        }

        [Fact]
        public void DefaultPageSize()
        {
            var searchForm = new SearchForm();
            SetupRepositorySearch(searchForm, 0);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(SearchForm.DefaultPageSize, result.PageSize);

            repository.VerifyAll();
        }

        [Fact]
        public void PreservesFormValues()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 2, PageSize = 7, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 0);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(searchForm.Query, result.Query);
            Assert.Equal(searchForm.IncludePrerelease, result.IncludePrerelease);
            Assert.Equal(searchForm.Page, result.Page);
            Assert.Equal(searchForm.PageSize, result.PageSize);

            repository.VerifyAll();
        }

        [Fact]
        public void CountsAllResults()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 2, PageSize = 7, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 1234);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(1234, result.TotalHits);

            repository.VerifyAll();
        }

        [Fact]
        public void LimitsResultsToOnePageSize()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 2, PageSize = 7, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 1234);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(searchForm.PageSize, result.Hits.Count());

            repository.VerifyAll();
        }

        [Fact]
        public void SkipsToCurrentPage()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 2, PageSize = 7, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 1234);
            var expected = (searchForm.Page * searchForm.PageSize).ToString();

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(expected, result.Hits.First().Id);

            repository.VerifyAll();
        }

        [Fact]
        public void SetsFirst()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 4, PageSize = 13, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 1234);
            var expectedFirst = searchForm.Page * searchForm.PageSize + 1;

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(expectedFirst, result.First);

            repository.VerifyAll();
        }

        [Fact]
        public void SetsLast()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 4, PageSize = 13, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 1234);
            var expectedLast = (searchForm.Page + 1) * searchForm.PageSize;

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(expectedLast, result.Last);

            repository.VerifyAll();
        }

        [Fact]
        public void SetsLastIncompletePage()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 0, PageSize = 10, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 8);
            const int expectedLast = 8;

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(expectedLast, result.Last);

            repository.VerifyAll();
        }

        [Fact]
        public void SetsLastPage()
        {
            var searchForm = new SearchForm { Query = "qsample", Page = 1, PageSize = 10, IncludePrerelease = true };
            SetupRepositorySearch(searchForm, 18);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.Equal(true, result.IsLastPage);

            repository.VerifyAll();
        }

        [Fact]
        public void ExecutesQuery()
        {
            var searchForm = new SearchForm();
            SetupRepositorySearch(searchForm, 10);

            var result = (SearchResultsViewModel)controller.Search(searchForm).Model;

            Assert.IsAssignableFrom<IList<IPackage>>(result.Hits);

            repository.VerifyAll();
        }

        private void SetupRepositorySearch(SearchForm searchForm, int numResultsToReturn)
        {
            var results = Enumerable.Range(0, numResultsToReturn).Select((p, i) => new LucenePackage(null, null) { Id = i.ToString(), IsLatestVersion = true}).ToList();
            
            repository.Setup(repo => repo.Search(searchForm.Query, new string[0], searchForm.IncludePrerelease))
                .Returns(results.AsQueryable()).Verifiable();
        }
    }
}
