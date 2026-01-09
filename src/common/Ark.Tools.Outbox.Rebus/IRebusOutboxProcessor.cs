namespace Ark.Tools.Outbox.Rebus;

internal interface IRebusOutboxProcessor
{
    void Start();
    void Stop();
}