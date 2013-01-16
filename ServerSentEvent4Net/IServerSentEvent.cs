using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    /// <summary>
    /// Functionallity for handling and sending a Server-Sent Events from ASP.NET WebApi.
    /// </summary>
    public interface IServerSentEvent
    {
        /// <summary>
        /// Raised when a new client is subrscribing.
        /// </summary>
        event EventHandler<SubscriberEventArgs> SubscriberAdded;
        /// <summary>
        /// Raised when a client has ended its subrscription.
        /// </summary>
        event EventHandler<SubscriberEventArgs> SubscriberRemoved;
        /// <summary>
        /// Makes a client a subscriber of this event.
        /// </summary>
        /// <param name="request">The incomming request from the client.</param>
        /// <returns>The response to send back to the client.</returns>
        HttpResponseMessage AddSubscriber(HttpRequestMessage request);
        /// <summary>
        /// Sends a message to all subscribers.
        /// </summary>
        /// <param name="data">The data to send.</param>
        void Send(string data);
        /// <summary>
        /// Sends a message to all subscribers.
        /// </summary>
        /// <param name="eventType">The type of message.</param>
        /// <param name="data">The data to send.</param>
        void Send(string eventType, string data);
        /// <summary>
        /// Sends a message to all subscribers.
        /// </summary>
        /// <param name="eventType">The type of message.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="messageId">Id of the message.</param>
        void Send(string eventType, string data, string messageId);
    }
}
