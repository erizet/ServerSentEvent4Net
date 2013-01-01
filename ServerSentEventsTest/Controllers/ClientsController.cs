using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;

namespace ServerSentEventsTest.Controllers
{
    public class ClientsController : ApiController
    {
        private static readonly ServerSentEvent SSE = new ServerSentEvent();

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            return SSE.Add(request);
        }

        internal static void Send(string text)
        {
            SSE.Send(text);

        }
    }
}
