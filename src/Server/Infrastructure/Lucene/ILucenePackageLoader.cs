namespace NuGet.Server.Infrastructure.Lucene
{
    public interface ILucenePackageLoader
    {
        LucenePackage LoadFromFileSystem(string path);
        LucenePackage Convert(IPackage package);
    }
}