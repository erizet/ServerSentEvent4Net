using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    /// <summary>
    /// A message sent by the Server-Sent Event.
    /// </summary>
    public interface IMessage
    {
        string Id { get; }
        string Data { get; }
        string EventType { get; }
        string Retry { get; }
    }
}
