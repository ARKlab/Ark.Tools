namespace Ark.Tools.ResourceWatcher.WorkerHost
{
    /// <summary>
    /// Rappresent a resource, including it's metadata and data (a parsed resource)
    /// </summary>
    /// <typeparam name="TMetadata">The Metadata class</typeparam>
    public interface IResource<TMetadata> : IResourceState
        where TMetadata : class, IResourceMetadata
    {
        TMetadata Metadata { get; }
    }
}
