using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Linq;
using System.ServiceModel.Web;
using Moq;
using NuGet;
using NuGet.Server.DataServices;
using NuGet.Server.Infrastructure;
using Xunit;

namespace Server.Test
{
    public class PackageSvcFacts
    {
        private readonly Mock<IServerPackageRepository> repository;
        private readonly Packages service;

        public PackageSvcFacts()
        {
            repository = new Mock<IServerPackageRepository>();
            service = new Packages(new Lazy<IServerPackageRepository>(() => repository.Object));
        }

        [Fact]
        public void EnsureAllDeclaredServicesAreRegistered()
        {
            // Arrange
            var registeredServices = new List<string>();
            var config = new Mock<IDataServiceConfiguration>(MockBehavior.Strict);
            config.Setup(s => s.SetServiceOperationAccessRule(It.IsAny<string>(), ServiceOperationRights.AllRead))
                  .Callback<string, ServiceOperationRights>((svc, _) => registeredServices.Add(svc));
            var expectedServices = typeof(Packages).GetMethods()
                                                     .Where(m => m.GetCustomAttributes(inherit: false).OfType<WebGetAttribute>().Any())
                                                     .Select(m => m.Name);

            // Act
            Packages.RegisterServices(config.Object);

            // Assert
            Assert.Equal(expectedServices.OrderBy(s => s, StringComparer.Ordinal),
                         registeredServices.OrderBy(s => s, StringComparer.Ordinal));
        }

        [Fact]
        public void SearchWithDistinctTargetFrameworks()
        {
            var result = new IPackage[0].AsQueryable();
            repository.Setup(repo => repo.Search("term", new[] {"net40", "net40-client"}, false)).Returns(result).Verifiable();

            service.Search("term", "net40|net40|net40-client|net40", false);
            
            repository.Verify();
        }
    }
}
