namespace Ark.Reference.Core.Application.Config
{
    public interface IRebusBusConfig
    {
        string? AsbConnectionString { get; }
        string? RequestQueue { get; }
        string? StorageConnectionString { get; }
    }
}