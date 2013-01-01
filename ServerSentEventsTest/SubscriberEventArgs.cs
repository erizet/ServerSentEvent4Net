using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerSentEventsTest
{
    class SubscriberEventArgs : EventArgs
    {
        public SubscriberEventArgs(int subscriberCount)
        {
            // TODO: Complete member initialization
            this.SubscriberCount = subscriberCount;
        }

        public int SubscriberCount { get; private set; }
    }
}
