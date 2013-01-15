using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    public class MemoryMessageHistory : IMessageHistory
    {
        private object mQueueLock = new object();
        private Queue<IMessage> mQueue = null;

        public MemoryMessageHistory(int noOfMessagesToRemember)
        {
            lock (mQueueLock)
            {
                mQueue = new Queue<IMessage>(noOfMessagesToRemember);
            }
        }

        public IMessage GetNextMessage(string messageId)
        {
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                lock (mQueueLock)
                {
                    var enumerator = mQueue.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        if (string.Compare(enumerator.Current.Id, messageId, true) == 0)
                        {
                            if (enumerator.MoveNext())
                                return enumerator.Current;
                        }
                    }
                }
            }
            return null;
        }

        public void Add(IMessage message)
        {
            lock (mQueueLock)
            {
                mQueue.Enqueue(message);
            }
        }
    }
}
