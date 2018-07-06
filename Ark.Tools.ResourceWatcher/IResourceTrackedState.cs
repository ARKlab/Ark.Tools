using NodaTime;

namespace Ark.Tools.ResourceWatcher
{
    public interface IResourceTrackedState : IResourceMetadata
    {
        int RetryCount { get; }
        Instant LastEvent { get; }
        string CheckSum { get; }
        Instant? RetrievedAt { get; }
    }
}
