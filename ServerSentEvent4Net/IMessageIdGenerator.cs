using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSentEvent4Net
{
    public interface IMessageIdGenerator
    {
        /// <summary>
        /// Generate a new id for the given message.
        /// </summary>
        /// <param name="msg">The message to generate an id for.</param>
        /// <returns>The generated id.</returns>
        string GetNextId(IMessage msg);
    }
}
