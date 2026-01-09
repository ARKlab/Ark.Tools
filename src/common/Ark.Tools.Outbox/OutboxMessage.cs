using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox(net10.0)', Before:
namespace Ark.Tools.Outbox
{

    public record OutboxMessage
    {
        /// <summary>
        /// Headers set by the Producer and used by the Consumer to propage the message to the Broker
        /// </summary>
        public Dictionary<string, string>? Headers { get; init; }
        /// <summary>
        /// Body of the message
        /// </summary>
        public byte[]? Body { get; init; }
    }
=======
namespace Ark.Tools.Outbox;


public record OutboxMessage
{
    /// <summary>
    /// Headers set by the Producer and used by the Consumer to propage the message to the Broker
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }
    /// <summary>
    /// Body of the message
    /// </summary>
    public byte[]? Body { get; init; }
>>>>>>> After


namespace Ark.Tools.Outbox;


public record OutboxMessage
{
    /// <summary>
    /// Headers set by the Producer and used by the Consumer to propage the message to the Broker
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }
    /// <summary>
    /// Body of the message
    /// </summary>
    public byte[]? Body { get; init; }
}