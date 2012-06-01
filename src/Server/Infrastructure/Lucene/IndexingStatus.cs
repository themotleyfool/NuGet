namespace NuGet.Server.Infrastructure.Lucene
{
    public enum IndexingState
    {
        Idle,
        Scanning,
        Building,
        Optimizing
    }

    public class IndexingStatus
    {
        public IndexingState State { get; set; }
        public string CurrentPackagePath { get; set; }
        public int CompletedPackages { get; set; }
        public int PackagesToIndex { get; set; }
    }
}