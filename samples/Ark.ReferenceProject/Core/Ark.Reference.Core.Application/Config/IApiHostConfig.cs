using Ark.Reference.Common.Services.FileStorageService;

namespace Ark.Reference.Core.Application.Config
{
    public interface IApiHostConfig
        : IRebusBusConfig
        , ICoreDataContextConfig
        , ICoreConfig
        , IFileStorageServiceConfig
    {
        string? SwaggerClientId { get; }
    }
}