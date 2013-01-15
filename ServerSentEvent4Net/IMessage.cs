using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    public interface IMessage
    {
        string Id { get; }
        string Data { get; }
        string EventType { get; }
        string Retry { get; }
    }
}
