using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;

namespace ServerSentEvent4Net
{
    /// <summary>
    /// Functionallity for handling and sending a Server-Sent Events from ASP.NET WebApi.
    /// </summary>
    /// <typeparam name="ClientInfo">Type to carry additional information for each client/subscriber.</typeparam>
    public class ServerSentEvent<ClientInfo> : ServerSentEvent
    {
        protected virtual List<ClientWithInformation<ClientInfo>> mClients = new List<ClientWithInformation<ClientInfo>>();

        
        public void Send(string data, Func<ClientInfo, bool> criteria) { Send(new Message() { Data = data }, criteria); }
        public void Send(string eventType, string data, Func<ClientInfo, bool> criteria) { Send(new Message() { EventType = eventType, Data = data }, criteria); }
        public void Send(string eventType, string data, string messageId, Func<ClientInfo, bool> criteria) { Send(new Message() { EventType = eventType, Data = data, Id = messageId }, criteria); }

        public HttpResponseMessage AddSubscriber(HttpRequestMessage request, ClientInfo clientInfo)
        {
            HttpResponseMessage response = request.CreateResponse();
            response.Content = new PushStreamContentWithClientInfomation<ClientInfo>(OnStreamAvailable, "text/event-stream", clientInfo);
            return response;
        }

        protected override void OnStreamAvailable(Stream stream, System.Net.Http.HttpContent content, System.Net.TransportContext context)
        {
            ClientInfo info = default(ClientInfo);

            if(content is PushStreamContentWithClientInfomation<ClientInfo>)
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
                var filtered = mClients.Where(c => criteria(c.Info)).ToList();
                filtered.ForEach(c => c.Send(msg));
                removed = mClients.RemoveAll(c => !c.IsConnected);
                count = filtered.Count;
            }

            if (removed > 0)
                OnSubscriberRemoved(count);

            _logger.Info("Message: " + msg.Data + " sent to " + count.ToString() + " clients.");
        }



        protected class ClientWithInformation<ClientInfo> : Client
        {
            public ClientWithInformation(Stream stream, string lastMessageId, ClientInfo clientInfo)
                : base(stream, lastMessageId)
            {
                this.Info = clientInfo;
            }

            public ClientWithInformation(Stream stream, ClientInfo clientInfo)
                : base(stream)
            {
                this.Info = clientInfo;
            }

            public ClientInfo Info { get; private set; }
        }

        protected class PushStreamContentWithClientInfomation<ClientInfo> : PushStreamContent
        {
            public PushStreamContentWithClientInfomation(Action<Stream, HttpContent, System.Net.TransportContext> onStreamAvailable, string mediaType, ClientInfo clientInfo)
                : base(onStreamAvailable, mediaType)
            {
                this.Info = clientInfo;
            }

            public ClientInfo Info { get; private set; }
        }
    }


    public class ServerSentEvent : IServerSentEvent
    {
        public event EventHandler<SubscriberEventArgs> SubscriberAdded;
        public event EventHandler<SubscriberEventArgs> SubscriberRemoved;

        protected virtual List<Client> mClients = new List<Client>();
        protected object mLock = new object();
        protected IMessageHistory mMessageHistory = null;
        protected IMessageIdGenerator mIdGenerator = null;
        protected static readonly slf4net.ILogger _logger = slf4net.LoggerFactory.GetLogger(typeof(ServerSentEvent));

        public ServerSentEvent(int noOfMessagesToRemember, bool generateMessageIds = false)
        {
            mMessageHistory = new MemoryMessageHistory(noOfMessagesToRemember);
            if (generateMessageIds)
                mIdGenerator = new SimpleIdGenerator();
        }

        public ServerSentEvent(IMessageHistory messageHistory, IMessageIdGenerator idGenerator)
        {
            if (messageHistory == null)
                throw new ArgumentException("messageHistory can not be null.");

            if (idGenerator == null)
                throw new ArgumentException("idGenerator can not be null.");

            mMessageHistory = messageHistory;
            mIdGenerator = idGenerator;
        }

        public HttpResponseMessage AddSubscriber(HttpRequestMessage request)
        {
            HttpResponseMessage response = request.CreateResponse();
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
        public void Send(string eventType, string data) { Send(new Message() { EventType = eventType, Data = data }); }
        public void Send(string eventType, string data, string messageId) { Send(new Message() { EventType = eventType, Data = data, Id = messageId }); }

        private void Send(Message msg)
        {
            // Add id?
            if (string.IsNullOrWhiteSpace(msg.Id) && mIdGenerator != null)
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


        protected class Message : IMessage
        {
            public string Id { get; set; }
            public string Data { get; set; }
            public string EventType { get; set; }
            public string Retry { get; set; }
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

                return sb.ToString();
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

                    LastMessageId = msg.Id;
                }
                catch (HttpException ex)
                {
                    if (ex.ErrorCode == -2147023667) // The remote host closed the connection. 
                    {
                        IsConnected = false;
                    }
                }
            }
        }
    }
}