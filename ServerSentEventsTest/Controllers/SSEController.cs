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

namespace ServerSentEventsTest.Controllers
{
    public class SSEController : ApiController
    {
        private static readonly Lazy<Timer> _timer = new Lazy<Timer>(() => new Timer(TimerCallback, null, 0, 1000));
        private static readonly ServerSentEvent SSE = new ServerSentEvent();

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            Timer t = _timer.Value;
            return SSE.Add(request);
        }


        private static void TimerCallback(object state)
        {
            Random randNum = new Random();
            SSE.Send(randNum.Next(30, 100).ToString());

            //To set timer with random interval
            _timer.Value.Change(TimeSpan.FromMilliseconds(randNum.Next(1, 3) * 500), TimeSpan.FromMilliseconds(-1));

        }
    }
}
