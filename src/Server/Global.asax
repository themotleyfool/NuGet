<%@ Application Inherits="System.Web.HttpApplication" Language="C#" %>
<%@ Import Namespace="System.ServiceModel.Activation" %>
<%@ Import Namespace="System.Web.Routing" %>
<%@ Import Namespace="Ninject" %>
<%@ Import Namespace="NuGet.Server" %>
<%@ Import Namespace="NuGet.Server.DataServices" %>
<%@ Import Namespace="NuGet.Server.Infrastructure" %>
<%@ Import Namespace="RouteMagic" %>

<script runat="server">
    
    protected void Application_Start(object sender, EventArgs e)
    {
        MapRoutes(RouteTable.Routes);
    }

    private static void MapRoutes(RouteCollection routes)
    {
        routes.MapDelegate("Redirect-Root",
                           "",
                           new DefaultUrlRedirect().Redirect);

        routes.MapDelegate("Redirect-Nuget-Action",
                           "nuget/{any}",
                           new DefaultUrlRedirect().Redirect);

        routes.MapDelegate("Redirect-Nuget",
                           "nuget",
                           new DefaultUrlRedirect().Redirect);

        // Route to create a new package
        routes.MapDelegate("CreatePackage-Base",
                           "api/v2",
                           new { httpMethod = new HttpMethodConstraint("PUT") },
                           context => CreatePackageService().CreatePackage(context.HttpContext));

        routes.MapDelegate("CreatePackage",
                           "api/v2/package",
                           new { httpMethod = new HttpMethodConstraint("PUT") },
                           context => CreatePackageService().CreatePackage(context.HttpContext));

        // Route to delete packages
        routes.MapDelegate("DeletePackage-Root",
                                       "{packageId}/{version}",
                                       new { httpMethod = new HttpMethodConstraint("DELETE") },
                                       context => CreatePackageService().DeletePackage(context.HttpContext));

        routes.MapDelegate("DeletePackage",
                           "api/v2/package/{packageId}/{version}",
                           new { httpMethod = new HttpMethodConstraint("DELETE") },
                           context => CreatePackageService().DeletePackage(context.HttpContext));

        // Route to get packages
        routes.MapDelegate("DownloadPackage",
                           "api/v2/package/{packageId}/{version}",
                           new { httpMethod = new HttpMethodConstraint("GET") },
                           context => CreatePackageService().DownloadPackage(context.HttpContext));

        // The default route is http://{root}/api/v2
        var factory = new System.Data.Services.DataServiceHostFactory();
        var serviceRoute = new ServiceRoute("api/v2", factory, typeof(Packages));
        serviceRoute.Defaults = new RouteValueDictionary { { "serviceType", "odata" } };
        serviceRoute.Constraints = new RouteValueDictionary { { "serviceType", "odata" } };
        routes.Add("nuget", serviceRoute);
    }

    private static PackageService CreatePackageService()
    {
        return NinjectBootstrapper.Kernel.Get<PackageService>();
    }
</script>
