using Ark.Tools.Compliance;

namespace Ark.Reference.Core.Application.Config;

public interface IRebusBusConfig
{
    [InfrastructureSecret]
    string? AsbConnectionString { get; }
    string? RequestQueue { get; }
    [InfrastructureSecret]
    string? StorageConnectionString { get; }
}