namespace Ark.Tools.Outbox.Rebus.Config;

public class OutboxOptions
{
    public int MaxMessagesPerBatch { get; set; } = 100;
    /// <summary>
    /// Defines if this Bus host the OutboxProcessor. Default: true
    /// </summary>
    public bool StartProcessor { get; set; } = true;
}
