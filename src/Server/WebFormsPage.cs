using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;

namespace NuGet.Server
{
    public class WebFormsPage : Page
    {
        private class DummyController : Controller {}

        public HtmlHelper Html { get; private set; }
        public UrlHelper Url { get; private set; }

        protected override void OnPreInit(EventArgs e)
        {
            base.OnPreRender(e);

            var httpContext = new HttpContextWrapper(Context);
            var routeData = new RouteData();

            var controllerContext = new ControllerContext(
                httpContext,
                routeData,
                new DummyController()
            );

            var viewContext = new ViewContext(
                controllerContext,
                new WebFormView(controllerContext, "Views"),
                new ViewDataDictionary(),
                new TempDataDictionary(),
                TextWriter.Null
            );

            Html = new HtmlHelper(viewContext, new ViewPage());
            Url = new UrlHelper(new RequestContext(httpContext, routeData));
        }
    }
}