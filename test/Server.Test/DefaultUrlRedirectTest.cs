using System.Web;
using System.Web.Routing;
using Moq;
using NuGet.Server;
using Xunit;

namespace Server.Test
{
    public class DefaultUrlRedirectTest
    {
        private readonly Mock<HttpContextBase> httpContext;
        private readonly Mock<HttpRequestBase> request;
        private readonly Mock<HttpResponseBase> response;
        private readonly RequestContext requestContext;
        private readonly DefaultUrlRedirect handler;

        public DefaultUrlRedirectTest()
        {
            httpContext = new Mock<HttpContextBase>();
            request = new Mock<HttpRequestBase>();
            response = new Mock<HttpResponseBase>();
            httpContext.Setup(c => c.Request).Returns(request.Object);
            httpContext.Setup(c => c.Response).Returns(response.Object);
            requestContext = new RequestContext(httpContext.Object, new RouteData());
            handler = new DefaultUrlRedirect();
        }

        [Fact]
        public void NoUserAgentRedirectToFeed()
        {
            // Arrange
            response.Setup(r => r.Redirect(DefaultUrlRedirect.FeedLocation, true)).Verifiable();

            // Act
            handler.Redirect(requestContext);

            // Assert
            response.Verify();
        }

        [Fact]
        public void NugetUserAgentRedirectToFeed()
        {
            // Arrange
            request.Setup(r => r.UserAgent).Returns("Some nuget client");
            response.Setup(r => r.Redirect(DefaultUrlRedirect.FeedLocation, true)).Verifiable();

            // Act
            handler.Redirect(requestContext);

            // Assert
            response.Verify();
        }

        [Fact]
        public void OtherUserAgentRedirectToWelcome()
        {
            // Arrange
            request.Setup(r => r.UserAgent).Returns("Mozilla/whatever");
            response.Setup(r => r.Redirect(DefaultUrlRedirect.WelcomeLocation, true)).Verifiable();

            // Act
            handler.Redirect(requestContext);

            // Assert
            response.Verify();
        }
    }
}
