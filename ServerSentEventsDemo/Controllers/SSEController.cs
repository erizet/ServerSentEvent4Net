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
        private static readonly Lazy<ServerSentEvent<InfoAboutSubscriber>> SSE = new Lazy<ServerSentEvent<InfoAboutSubscriber>>(() =>
        {
            var sse = new ServerSentEvent<InfoAboutSubscriber>(10, true, 5000);
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
            return SSE.Value.AddSubscriber(request, new InfoAboutSubscriber("both"));
        }

        public HttpResponseMessage Get(string filter)
        {
            Timer t = _timer.Value;
            return SSE.Value.AddSubscriber(this.Request, new InfoAboutSubscriber(filter));
        }


        private static void TimerCallback(object state)
        {
            Random randNum = new Random();
            int num = randNum.Next(30, 100);
            SSE.Value.Send(num.ToString(), info =>
            {
                if (info != null)
                {
                    bool isOdd = (num % 2) == 1;

                    switch (info.Filter)
                    {
                        case InfoAboutSubscriber.MessageFilter.Both:
                            return true;
                        case InfoAboutSubscriber.MessageFilter.Odd:
                            return isOdd;
                        case InfoAboutSubscriber.MessageFilter.Even:
                            return !isOdd;
                    }
                }
                return false;
            });
            //To set timer with random interval
            _timer.Value.Change(TimeSpan.FromMilliseconds(randNum.Next(1, 3) * 500), TimeSpan.FromMilliseconds(-1));

        }

        private class InfoAboutSubscriber
        {
            public enum MessageFilter
            {
                Both, Odd, Even
            }

            public InfoAboutSubscriber(string filter)
            {
                if (String.Compare(filter, "odd", true) == 0)
                    this.Filter = MessageFilter.Odd;
                else if(String.Compare(filter, "even", true) == 0)
                    this.Filter = MessageFilter.Even;
                else
                    this.Filter = MessageFilter.Both;

            }
            public MessageFilter Filter { get; private set; }
        }
    }
}
