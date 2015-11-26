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
    public class ServerSentEvent : IServerSentEvent
    {
        public event EventHandler<SubscriberEventArgs> SubscriberAdded;
        public event EventHandler<SubscriberEventArgs> SubscriberRemoved;

        protected List<Client> mClients = new List<Client>();
        protected object mLock = new object();
        protected IMessageHistory mMessageHistory = null;
        protected IMessageIdGenerator mIdGenerator = null;
        protected static readonly slf4net.ILogger _logger = slf4net.LoggerFactory.GetLogger(typeof(ServerSentEvent));
        protected int mHeartbeatInterval = 0;
        protected Timer mHeartbeatTimer = null;


        public ServerSentEvent(int noOfMessagesToRemember, bool generateMessageIds = false, int heartbeatInterval = 0)
        {
            mHeartbeatInterval = heartbeatInterval;
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
            mHeartbeatInterval = heartbeatInterval;

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
            lock (mLock)
            {
                SendAndRemoveDisconneced(mClients, msg);
            }
        }

        protected void SendAndRemoveDisconneced(List<Client> clientsToSendTo, Message msg)
        {
            CheckMessage(msg);

            int removed = 0;
            int count = 0;
            lock (mLock)
            {
                clientsToSendTo.ForEach(c => c.Send(msg));
                removed = mClients.RemoveAll(c => !c.IsConnected);
                count = mClients.Count;
            }

            if (removed > 0)
                OnSubscriberRemoved(count);

            if (Message.IsOnlyComment(msg))
                _logger.Trace("Comment: " + msg.Comment + " sent to " + count.ToString() + " clients.");
            else
                _logger.Info("Message: " + msg.Data + " sent to " + count.ToString() + " clients.");
        }

        protected void CheckMessage(Message msg)
        {
            // Add id?
            if (string.IsNullOrWhiteSpace(msg.Id) && mIdGenerator != null && !Message.IsOnlyComment(msg))
                msg.Id = mIdGenerator.GetNextId(msg);

            // Add retry?
            if (mHeartbeatTimer != null)
                msg.Retry = mHeartbeatInterval.ToString();
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
            if (heartbeatInterval > 0)
            {
                mHeartbeatTimer = new Timer(TimerCallback, null, 1000, heartbeatInterval);
            }
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
            public bool IsRetrySent { get; private set; }
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
                    // Only send retry once for each connection
                    if (IsRetrySent)
                        msg.Retry = null;

                    var text = msg.ToString();
                    StreamWriter.WriteLine(text);
                    StreamWriter.Flush();

                    if (!Message.IsOnlyComment(msg))
                        LastMessageId = msg.Id;

                    if (!string.IsNullOrWhiteSpace(msg.Retry))
                        IsRetrySent = true;
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
                catch (IOException)
                {
                    IsConnected = false;
                }
                catch (ObjectDisposedException)
                {
                    IsConnected = false;
                }
            }
        }
    }
}