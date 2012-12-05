using System;
using System.Threading.Tasks;

namespace NuGet.Server.Infrastructure.Lucene
{
    public interface IPackageIndexer
    {
        /// <summary>
        /// Gets status of index building activity.
        /// </summary>
        IndexingStatus GetIndexingStatus();

        Task SynchronizeIndexWithFileSystem();
        Task AddPackage(LucenePackage package);
        Task RemovePackage(IPackage package);
        Task IncrementDownloadCount(IPackage package);

        void Optimize();
    }
}