namespace NuGet.Server.Infrastructure.Lucene
{
    public interface ILucenePackageLoader
    {
        /// <summary>
        /// Loads pacakge data from the Lucene index with a given path.
        /// </summary>
        LucenePackage LoadFromIndex(string path);

        /// <summary>
        /// Loads a package from disk with a given path, then
        /// converts it to a <c cref="LucenePackage"/> using <c cref="Convert"/>.
        /// </summary>
        LucenePackage LoadFromFileSystem(string path);

        /// <summary>
        /// Retrieves <c cref="DerivedPackageData"/> for
        /// a given generic <c cref="IPackage"/> and returns
        /// a <c cref="LucenePackage"/> that holds the aggregate
        /// data.
        /// </summary>
        LucenePackage Convert(IPackage package);
    }
}