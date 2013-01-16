using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;

namespace ServerSentEvent4Net
{
    public class ServerSentEvent : IServerSentEvent
    {
        public event EventHandler<SubscriberEventArgs> SubscriberAdded;
        public event EventHandler<SubscriberEventArgs> SubscriberRemoved;

        protected List<Client> mClients = new List<Client>();
        protected object mLock = new object();
        protected IMessageHistory mMessageHistory = null;
        protected IMessageIdGenerator mIdGenerator = null;
        private static readonly slf4net.ILogger _logger = slf4net.LoggerFactory.GetLogger(typeof(ServerSentEvent));

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

        protected void OnStreamAvailable(Stream stream, System.Net.Http.HttpContent content, System.Net.TransportContext context)
        {
            int count = 0;
            string lastMessageId = GetLastMessageId(content);
            Client client = new Client(stream, lastMessageId);
            lock (mLock)
            {
                mClients.Add(client);
                count = mClients.Count; 
            }
            
            OnSubscriberAdded(count);

            // Send all messages since LastMessageId
            IMessage nextMessage = null;
            while((nextMessage = mMessageHistory.GetNextMessage(client.LastMessageId)) != null)
                client.Send(nextMessage);
        }

        private string GetLastMessageId(HttpContent content)
        {
            string id = string.Empty;
            IEnumerable<string> values = new List<string>();
            if(content.Headers.TryGetValues(@"Last-Event-ID", out values))
                id= values.FirstOrDefault();

            return id;
        }

        public void Send(string data) { Send(new Message() { Data = data }); }
        public void Send(string eventType, string data) { Send(new Message() { EventType = eventType, Data = data }); }
        public void Send(string eventType, string data, string messageId) { Send(new Message() { EventType = eventType, Data = data, Id = messageId }); }

        protected void Send(Message msg)
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

            public Client(Stream stream, string lastMessageId) : this(stream)
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