using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerSentEvent4Net
{
    public class SubscriberEventArgs : EventArgs
    {
        public SubscriberEventArgs(int subscriberCount)
        {
            // TODO: Complete member initialization
            this.SubscriberCount = subscriberCount;
        }

        public int SubscriberCount { get; private set; }
    }
}
