using Ark.Reference.Common.Services.FileStorageService;

namespace Core.Service.Application.Config
{
    public interface IApiHostConfig 
        : IRebusBusConfig
        , ICoreDataContextConfig
        , ICoreConfig
        , IFileStorageServiceConfig
    {
        string SwaggerClientId { get; }
    }
}
