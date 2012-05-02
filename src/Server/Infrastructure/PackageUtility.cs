using System;
using System.Configuration;
using System.Web;
using System.Web.Hosting;
using System.Web.Routing;
using NuGet.Server.DataServices;

namespace NuGet.Server.Infrastructure
{
    public class PackageUtility
    {
        private static readonly Lazy<string> _luceneIndexPhysicalPath = new Lazy<string>(ResolveLuceneIndexPath);
        private static readonly Lazy<string> _packagePhysicalPath = new Lazy<string>(ResolvePackagePath);

        public static string PackagePhysicalPath
        {
            get
            {
                return _packagePhysicalPath.Value;
            }
        }

        public static string LuceneIndexPhysicalPath
        {
            get
            {
                return _luceneIndexPhysicalPath.Value;
            }
        }

        public static string GetPackageDownloadUrl(Package package)
        {
            var routesValues = new RouteValueDictionary { 
                { "packageId", package.Id },
                { "version", package.Version.ToString() } 
            };

            var context = HttpContext.Current;

            RouteBase route = RouteTable.Routes["DownloadPackage"];

            var vpd = route.GetVirtualPath(context.Request.RequestContext, routesValues);

            string applicationPath = Helpers.EnsureTrailingSlash(context.Request.ApplicationPath);

            return applicationPath + vpd.VirtualPath;
        }

        private static string ResolvePackagePath()
        {
            return MapPathFromAppSetting("packagesPath", "~/Packages");
        }

        private static string ResolveLuceneIndexPath()
        {
            return MapPathFromAppSetting("lucenePath", "~/Lucene");
        }

        private static string MapPathFromAppSetting(string key, string defaultValue)
        {
            var path = ConfigurationManager.AppSettings[key];

            if (String.IsNullOrEmpty(path))
            {
                path = defaultValue;
            }

            if (path.StartsWith("~/"))
            {
                return HostingEnvironment.MapPath(path);
            }

            return path;
        }
    }
}
