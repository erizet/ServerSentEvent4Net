using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    public interface IMessageIdGenerator
    {
        string GetNextId(IMessage msg);
    }
}
