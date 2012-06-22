namespace NuGet.Server.Infrastructure
{
    /// <summary>
    /// Groups packages into subfolders based on their id so that
    /// all versions of a package go into the same subfolder.
    /// </summary>
    public class OneFolderPerPackageIdPathResolver : DefaultPackagePathResolver
    {
        public OneFolderPerPackageIdPathResolver(string path)
            : base(path)
        {
        }

        public override string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            return packageId;
        }
    }
}