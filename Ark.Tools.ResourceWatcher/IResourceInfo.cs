using NodaTime;

namespace Ark.Tools.ResourceWatcher
{
    public interface IResourceMetadata
    {
        /// <summary>
        /// The "key" identifier of the resource
        /// </summary>
        string ResourceId { get; }
        /// <summary>
        /// The "version" of the resource. Used to avoid retrival of the resource in case if the same version is already been processed successfully
        /// </summary>
        LocalDateTime Modified { get; }
        /// <summary>
        /// Additional info serialized to the State tracking
        /// </summary>
        object Extensions { get; }
    }
}
