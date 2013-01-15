using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ServerSentEvent4Net
{
    public class SimpleIdGenerator : IMessageIdGenerator
    {
        private int mCounter = 0;

        public string GetNextId(IMessage msg)
        {
            return Interlocked.Increment(ref mCounter).ToString();
        }
    }
}
