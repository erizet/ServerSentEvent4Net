using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web;

namespace ServerSentEvent4Net
{
    /// <summary>
    /// Functionallity for handling and sending a Server-Sent Events from ASP.NET WebApi.
    /// </summary>
    /// <typeparam name="ClientInfo">Type to carry additional information for each client/subscriber.</typeparam>
    public class ServerSentEvent<ClientInfo> : ServerSentEvent
    {
        public ServerSentEvent(int noOfMessagesToRemember, bool generateMessageIds = false, int heartbeatInterval = 0)
            : base(noOfMessagesToRemember, generateMessageIds)
        { }
        public ServerSentEvent(IMessageHistory messageHistory, IMessageIdGenerator idGenerator, int heartbeatInterval = 0)
            : base(messageHistory, idGenerator)
        { }

        /// <summary>
        /// Sends data to all subscribers fulfilling the criteria.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="criteria">The criteria to be fulfilled to get the data.</param>
        public void Send(string data, Func<ClientInfo, bool> criteria) { Send(new Message() { Data = data }, criteria); }
        /// <summary>
        /// Sends data to all subscribers fulfilling the criteria.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="messageId">The id of the message.</param>
        /// <param name="criteria">The criteria to be fulfilled to get the data.</param>
        public void Send(string data, string eventType, Func<ClientInfo, bool> criteria) { Send(new Message() { EventType = eventType, Data = data }, criteria); }
        /// <summary>
        /// Sends data to all subscribers fulfilling the criteria.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="messageId">The id of the message.</param>
        /// <param name="criteria">The criteria to be fulfilled to get the data.</param>
        public void Send(string data, string eventType, string messageId, Func<ClientInfo, bool> criteria) { Send(new Message() { EventType = eventType, Data = data, Id = messageId }, criteria); }

        public HttpResponseMessage AddSubscriber(HttpRequestMessage request, ClientInfo clientInfo)
        {
            HttpResponseMessage response = request.CreateResponse();
            response.Content = new PushStreamContentWithClientInfomation<ClientInfo>(OnStreamAvailable, "text/event-stream", clientInfo);
            return response;
        }

        protected override void OnStreamAvailable(Stream stream, System.Net.Http.HttpContent content, System.Net.TransportContext context)
        {
            ClientInfo info = default(ClientInfo);

            if (content is PushStreamContentWithClientInfomation<ClientInfo>)
            {
                PushStreamContentWithClientInfomation<ClientInfo> contentWithInfo = content as PushStreamContentWithClientInfomation<ClientInfo>;
                info = contentWithInfo.Info;
            }

            string lastMessageId = GetLastMessageId(content);
            ClientWithInformation<ClientInfo> client = new ClientWithInformation<ClientInfo>(stream, lastMessageId, info);

            AddClient(client);

        }

        private void Send(Message msg, Func<ClientInfo, bool> criteria)
        {
            // Add id?
            if (string.IsNullOrWhiteSpace(msg.Id) && mIdGenerator != null)
                msg.Id = mIdGenerator.GetNextId(msg);

            int removed = 0;
            int count = 0;
            lock (mLock)
            {
                // Only send message to clients fullfilling the criteria
                var filtered = mClients
                                .Where(c => c is ClientWithInformation<ClientInfo>)
                                    .Where(c =>
                                    {
                                        var clientWithInfo = c as ClientWithInformation<ClientInfo>;
                                        return clientWithInfo.Info == null ? false : criteria(clientWithInfo.Info);
                                    }).ToList();
                filtered.ForEach(c => c.Send(msg));
                removed = mClients.RemoveAll(c => !c.IsConnected);
                count = filtered.Count;
            }

            if (removed > 0)
                OnSubscriberRemoved(count);

            _logger.Info("Message: " + msg.Data + " sent to " + count.ToString() + " clients.");
        }



        protected class ClientWithInformation<Data> : Client
        {
            public ClientWithInformation(Stream stream, string lastMessageId, Data clientInfo)
                : base(stream, lastMessageId)
            {
                this.Info = clientInfo;
            }

            public ClientWithInformation(Stream stream, Data clientInfo)
                : base(stream)
            {
                this.Info = clientInfo;
            }

            public Data Info { get; private set; }
        }

        protected class PushStreamContentWithClientInfomation<Data> : PushStreamContent
        {
            public PushStreamContentWithClientInfomation(Action<Stream, HttpContent, System.Net.TransportContext> onStreamAvailable, string mediaType, Data clientInfo)
                : base(onStreamAvailable, mediaType)
            {
                this.Info = clientInfo;
            }

            public Data Info { get; private set; }
        }
    }


    public class ServerSentEvent : IServerSentEvent
    {
        public event EventHandler<SubscriberEventArgs> SubscriberAdded;
        public event EventHandler<SubscriberEventArgs> SubscriberRemoved;

        protected List<Client> mClients = new List<Client>();
        protected object mLock = new object();
        protected IMessageHistory mMessageHistory = null;
        protected IMessageIdGenerator mIdGenerator = null;
        protected static readonly slf4net.ILogger _logger = slf4net.LoggerFactory.GetLogger(typeof(ServerSentEvent));
        protected Timer mHeartbeatTimer = null;


        public ServerSentEvent(int noOfMessagesToRemember, bool generateMessageIds = false, int heartbeatInterval = 0)
        {
            mMessageHistory = new MemoryMessageHistory(noOfMessagesToRemember);
            if (generateMessageIds)
                mIdGenerator = new SimpleIdGenerator();
            SetupHeartbeat(heartbeatInterval);
        }

        public ServerSentEvent(IMessageHistory messageHistory, IMessageIdGenerator idGenerator, int heartbeatInterval = 0)
        {
            if (messageHistory == null)
                throw new ArgumentException("messageHistory can not be null.");

            if (idGenerator == null)
                throw new ArgumentException("idGenerator can not be null.");

            mMessageHistory = messageHistory;
            mIdGenerator = idGenerator;

            SetupHeartbeat(heartbeatInterval);
        }

        public HttpResponseMessage AddSubscriber(HttpRequestMessage request)
        {
            HttpResponseMessage response = request.CreateResponse();
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache, must-revalidate");
            response.Content = new PushStreamContent(OnStreamAvailable, "text/event-stream");
            return response;
        }

        protected virtual void OnStreamAvailable(Stream stream, System.Net.Http.HttpContent content, System.Net.TransportContext context)
        {
            string lastMessageId = GetLastMessageId(content);
            Client client = new Client(stream, lastMessageId);

            AddClient(client);
        }

        protected void AddClient(Client client)
        {
            int count = 0;
            lock (mLock)
            {
                mClients.Add(client);
                count = mClients.Count;
            }

            OnSubscriberAdded(count);

            // Send all messages since LastMessageId
            IMessage nextMessage = null;
            while ((nextMessage = mMessageHistory.GetNextMessage(client.LastMessageId)) != null)
                client.Send(nextMessage);
        }

        protected string GetLastMessageId(HttpContent content)
        {
            string id = string.Empty;
            IEnumerable<string> values = new List<string>();
            if (content.Headers.TryGetValues(@"Last-Event-ID", out values))
                id = values.FirstOrDefault();

            return id;
        }

        public void Send(string data) { Send(new Message() { Data = data }); }
        public void Send(string data, string eventType) { Send(new Message() { EventType = eventType, Data = data }); }
        public void Send(string data, string eventType, string messageId) { Send(new Message() { EventType = eventType, Data = data, Id = messageId }); }

        private void Send(Message msg)
        {
            // Add id?
            if (string.IsNullOrWhiteSpace(msg.Id) && mIdGenerator != null && !Message.IsOnlyComment(msg))
                msg.Id = mIdGenerator.GetNextId(msg);

            int removed = 0;
            int count = 0;
            lock (mLock)
            {
                mClients.ForEach(c => c.Send(msg));
                removed = mClients.RemoveAll(c => !c.IsConnected);
                count = mClients.Count;
            }

            if (removed > 0)
                OnSubscriberRemoved(count);

            if(Message.IsOnlyComment(msg))
                _logger.Trace("Comment: " + msg.Comment + " sent to " + count.ToString() + " clients.");
            else
                _logger.Info("Message: " + msg.Data + " sent to " + count.ToString() + " clients.");
        }

        protected void OnSubscriberAdded(int subscriberCount)
        {
            _logger.Info("Subscriber added. No of subscribers: " + subscriberCount);

            if (SubscriberAdded != null)
                SubscriberAdded(this, new SubscriberEventArgs(subscriberCount));
        }

        protected void OnSubscriberRemoved(int subscriberCount)
        {
            _logger.Info("Subscriber removed. No of subscribers: " + subscriberCount);

            if (SubscriberRemoved != null)
                SubscriberRemoved(this, new SubscriberEventArgs(subscriberCount));
        }

        protected void SetupHeartbeat(int heartbeatInterval)
        {
            if(heartbeatInterval > 0)
                mHeartbeatTimer = new Timer(TimerCallback, null, 1000, heartbeatInterval);
        }

        private void TimerCallback(object state)
        {
            Send(new Message() { Comment = "heartbeat" });
        }


        protected class Message : IMessage
        {
            public string Id { get; set; }
            public string Data { get; set; }
            public string EventType { get; set; }
            public string Retry { get; set; }
            public string Comment { get; set; }
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                if (!String.IsNullOrEmpty(Id))
                    sb.Append("id: ").AppendLine(Id);
                if (!String.IsNullOrEmpty(EventType))
                    sb.Append("event: ").AppendLine(EventType);
                if (!String.IsNullOrEmpty(Data))
                    sb.Append("data: ").AppendLine(Data);
                if (!String.IsNullOrEmpty(Retry))
                    sb.Append("retry: ").AppendLine(Retry);
                if (!String.IsNullOrEmpty(Comment))
                    sb.Append(": ").AppendLine(Comment);

                return sb.ToString();
            }

            public static bool IsOnlyComment(IMessage msg)
            {
                    return String.IsNullOrEmpty(msg.Id) &&
                           String.IsNullOrEmpty(msg.EventType) &&
                           String.IsNullOrEmpty(msg.Data) &&
                           String.IsNullOrEmpty(msg.Retry) &&
                           !String.IsNullOrEmpty(msg.Comment);
            }
        }

        protected class Client
        {
            public StreamWriter StreamWriter { get; private set; }
            public bool IsConnected { get; private set; }
            public string LastMessageId { get; private set; }

            public Client(Stream stream, string lastMessageId)
                : this(stream)
            {
                this.LastMessageId = lastMessageId;
            }

            public Client(Stream stream)
            {
                StreamWriter streamwriter = new StreamWriter(stream);
                this.StreamWriter = streamwriter;
                IsConnected = true;
            }

            public void Send(IMessage msg)
            {
                try
                {
                    var text = msg.ToString();
                    StreamWriter.WriteLine(text);
                    StreamWriter.Flush();

                    if(!Message.IsOnlyComment(msg))
                        LastMessageId = msg.Id;
                }
                catch (HttpException ex)
                {
                    if (ex.ErrorCode == -2147023667) // The remote host closed the connection. 
                    {
                        IsConnected = false;
                    }
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    IsConnected = false;
                }
            }
        }
    }
}