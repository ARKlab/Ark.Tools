using Ark.Tools.Compliance;

namespace Ark.Reference.Core.Application.Config;

public interface IRebusBusConfig
{
    [Secret]
    string? AsbConnectionString { get; }
    string? RequestQueue { get; }
    [Secret]
    string? StorageConnectionString { get; }
}