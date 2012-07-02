using NuGet.Server.Infrastructure.Lucene;

namespace Server.Test.Lucene
{
    public abstract class PackageIndexerTestBase : TestBase
    {
        protected readonly PackageIndexer indexer;

        protected PackageIndexerTestBase()
        {
            indexer = new PackageIndexer
                          {
                              FileSystem = fileSystem.Object,
                              Provider = provider,
                              Writer = indexWriter,
                              PackageLoader = loader.Object
                          };
        }
    }
}