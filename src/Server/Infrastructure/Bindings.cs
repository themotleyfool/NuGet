using Ninject.Modules;
using NuGet.Server.Infrastructure.Lucene;

namespace NuGet.Server.Infrastructure
{
    public class Bindings : NinjectModule
    {
        public override void Load()
        {
            IServerPackageRepository packageRepository = new LucenePackageRepository(PackageUtility.PackagePhysicalPath, PackageUtility.LuceneIndexPhysicalPath);
            Bind<IHashProvider>().To<CryptoHashProvider>();
            Bind<IServerPackageRepository>().ToConstant(packageRepository);
            Bind<PackageService>().ToSelf();
            Bind<IPackageAuthenticationService>().To<PackageAuthenticationService>();
        }
    }
}
