using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;

namespace ServerSentEventsTest
{
    internal class ServerSentEvent
    {
        protected List<StreamWriter> m_streams = new List<StreamWriter>();
        protected object mLock = new object();

        public HttpResponseMessage Add(HttpRequestMessage request)
        {
            HttpResponseMessage response = request.CreateResponse();
            response.Content = new PushStreamContent(OnStreamAvailable, "text/event-stream");
            return response;
        }

        protected void OnStreamAvailable(Stream stream, System.Net.Http.HttpContent content, System.Net.TransportContext context)
        {
            StreamWriter streamwriter = new StreamWriter(stream);
            lock (mLock)
            {
                m_streams.Add(streamwriter);
            }
        }

        public void Send(string data)
        {
            Send(new Message() { Data = data });
        }
        public void Send(string eventType, string data)
        {
            Send(new Message() { EventType = eventType, Data = data });
        }

        protected void Send(Message msg)
        {
            List<StreamWriter> toRemove = new List<StreamWriter>();

            lock (mLock)
            {
                foreach (var s in m_streams)
                {
                    try
                    {
                        s.WriteLine(msg.ToString());
                        s.Flush();
                    }
                    catch (HttpException ex)
                    {
                        if (ex.ErrorCode == -2147023667) // The remote host closed the connection. 
                        {
                            toRemove.Add(s);
                        }
                    }
                }

                foreach (var s in toRemove)
                {
                    m_streams.Remove(s);
                }
            }
        }


        protected class Message
        {
            public string Data { get; set; }
            public string EventType { get; set; }
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                if (!String.IsNullOrEmpty(EventType))
                    sb.Append("event: ").AppendLine(EventType);
                if (!String.IsNullOrEmpty(Data))
                    sb.Append("data: ").AppendLine(Data);

                return sb.ToString();
            }
        }
    }
}