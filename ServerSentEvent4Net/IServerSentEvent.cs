using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    public interface IServerSentEvent
    {
        event EventHandler<SubscriberEventArgs> SubscriberAdded;
        event EventHandler<SubscriberEventArgs> SubscriberRemoved;
        HttpResponseMessage AddSubscriber(HttpRequestMessage request);
        void Send(string data);
        void Send(string eventType, string data);
    }
}
