using System;

namespace NuGet.Server.Infrastructure.Lucene
{
    public enum IndexingState
    {
        Idle,
        Scanning,
        Building,
        Commit,
        Optimizing
    }

    public class IndexingStatus
    {
        public IndexingState State { get; set; }
        public string CurrentPackagePath { get; set; }
        public int CompletedPackages { get; set; }
        public int PackagesToIndex { get; set; }
        public int TotalPackages { get; set; }
        public int PendingDeletes { get; set; }
        public bool IsOptimized { get; set; }
        public DateTime LastModification { get; set; }
    }
}