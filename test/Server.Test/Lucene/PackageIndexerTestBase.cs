using Lucene.Net.Linq;
using NuGet.Server.Infrastructure.Lucene;

namespace Server.Test.Lucene
{
    public abstract class PackageIndexerTestBase : TestBase
    {
        protected readonly TestablePackageIndexer indexer;

        protected PackageIndexerTestBase()
        {
            indexer = new TestablePackageIndexer
                          {
                              FileSystem = fileSystem.Object,
                              Provider = provider,
                              Writer = indexWriter,
                              PackageRepository = loader.Object
                          };
        }

        public class TestablePackageIndexer : PackageIndexer
        {
            public ISession<LucenePackage> FakeSession { get; set; } 

            protected internal override ISession<LucenePackage> OpenSession()
            {
                return FakeSession ?? base.OpenSession();
            }
        }
    }
}