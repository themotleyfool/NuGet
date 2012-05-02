using System.Web.Routing;

namespace NuGet.Server
{
    internal class DefaultUrlRedirect
    {
        public void Redirect(RequestContext context)
        {
            var location = "~/Default.aspx";

            var userAgent = context.HttpContext.Request.UserAgent;
            if (userAgent == null || userAgent.ToLowerInvariant().Contains("nuget"))
            {
                location = "~/api/v2";
            }

            context.HttpContext.Response.Redirect(location, true);
        }
    }
}