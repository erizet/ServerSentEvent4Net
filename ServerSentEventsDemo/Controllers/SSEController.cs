using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web;
using System.Web.Http;
using ServerSentEvent4Net;

namespace ServerSentEventsTest.Controllers
{
    public class SSEController : ApiController
    {
        private static readonly Lazy<Timer> _timer = new Lazy<Timer>(() => new Timer(TimerCallback, null, 0, 1000));
        //private static readonly ServerSentEvent SSE = new ServerSentEvent();
        private static readonly Lazy<ServerSentEvent> SSE = new Lazy<ServerSentEvent>(() =>
        {
            var sse = new ServerSentEvent(10);
            sse.SubscriberAdded += SSE_SubscriberChanged;
            sse.SubscriberRemoved += SSE_SubscriberChanged;
            return sse;
        });


        static void SSE_SubscriberChanged(object sender, SubscriberEventArgs e)
        {
            ClientsController.Send(e.SubscriberCount.ToString());
        }

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            Timer t = _timer.Value;
            return SSE.Value.AddSubscriber(request);
        }


        private static void TimerCallback(object state)
        {
            Random randNum = new Random();
            SSE.Value.Send(randNum.Next(30, 100).ToString());

            //To set timer with random interval
            _timer.Value.Change(TimeSpan.FromMilliseconds(randNum.Next(1, 3) * 500), TimeSpan.FromMilliseconds(-1));

        }
    }
}
