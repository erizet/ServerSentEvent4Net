using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using ServerSentEvent4Net;

namespace ServerSentEventsTest.Controllers
{
    public class ClientsController : ApiController
    {
        private static readonly Lazy<ServerSentEvent> SSE = new Lazy<ServerSentEvent>(() => {
            var sse = new ServerSentEvent(10);
            sse.SubscriberAdded += SSE_SubscriberChanged;
            sse.SubscriberRemoved += SSE_SubscriberChanged;
            return sse;
        });

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            return SSE.Value.AddSubscriber(request);
        }

        internal static void Send(string text)
        {
            SSE.Value.Send(text);
        }

        static void SSE_SubscriberChanged(object sender, SubscriberEventArgs e)
        {
            SSE.Value.Send(e.SubscriberCount.ToString());
        }

    }
}
