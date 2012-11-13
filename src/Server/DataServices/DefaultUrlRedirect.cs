using System.Web.Routing;

namespace NuGet.Server
{
    public class DefaultUrlRedirect
    {
        public const string FeedLocation = "~/api/v2";
        public const string WelcomeLocation = "~/home";

        public void Redirect(RequestContext context)
        {
            var location = WelcomeLocation;

            var userAgent = context.HttpContext.Request.UserAgent;
            if (userAgent == null || userAgent.ToLowerInvariant().Contains("nuget"))
            {
                location = FeedLocation;
            }

            context.HttpContext.Response.Redirect(location, true);
        }
    }
}