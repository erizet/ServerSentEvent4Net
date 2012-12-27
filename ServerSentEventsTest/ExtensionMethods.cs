using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ServerSentEventsTest
{
    public static class ExtensionMethods
    {
        public static string ToPublicUrl(this System.Web.Mvc.UrlHelper urlHelper, string relativeUri)
        {
            var httpContext = urlHelper.RequestContext.HttpContext;

            var uriBuilder = new UriBuilder
            {
                Host = httpContext.Request.Url.Host,
                Path = "/",
                Port = 80,
                Scheme = "http",
            };

            if (httpContext.Request.IsLocal)
            {
                uriBuilder.Port = httpContext.Request.Url.Port;
            }

            return new Uri(uriBuilder.Uri, relativeUri).AbsoluteUri;
        }
        public static string ToPublicUrl(this System.Web.Http.Routing.UrlHelper urlHelper, Uri relativeUri)
        {
            var requestUrl = urlHelper.Request.RequestUri;

            var uriBuilder = new UriBuilder
            {
                Host = requestUrl.Host,
                Path = "/",
                Port = 80,
                Scheme = "http",
            };

#if DEBUG
            uriBuilder.Port = requestUrl.Port;
#endif

            return new Uri(uriBuilder.Uri, relativeUri).AbsoluteUri;
        }
    }
}