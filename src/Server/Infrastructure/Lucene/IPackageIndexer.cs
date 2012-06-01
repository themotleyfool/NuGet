using System;

namespace NuGet.Server.Infrastructure.Lucene
{
    public interface IPackageIndexer
    {
        /// <summary>
        /// Gets status of index building activity.
        /// </summary>
        IndexingStatus GetIndexingStatus();

        IAsyncResult BeginSynchronizeIndexWithFileSystem(AsyncCallback callback, object state);
        void EndSynchronizeIndexWithFileSystem(IAsyncResult ar);
        void SynchronizeIndexWithFileSystem();
        void AddPackage(LucenePackage package);
        void RemovePackage(IPackage package);
        void IncrementDownloadCount(IPackage package);
    }
}