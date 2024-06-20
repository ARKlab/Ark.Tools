namespace Ark.Reference.Common.Services.FileStorageService
{
    public interface IFileStorageServiceConfig
    {
        string StorageAccount { get; }
        string StoragePrefix { get; }
    }
}
