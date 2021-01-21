using Ark.Tools.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{

    public class OutboxMessage
    {
        /// <summary>
        /// Headers set by the Producer and used by the Consumer to propage the message to the Broker
        /// </summary>
        public Dictionary<string,string> Headers { get; set; }
        /// <summary>
        /// Body of the message
        /// </summary>
        public byte[] Body { get; set; }
    }
}
