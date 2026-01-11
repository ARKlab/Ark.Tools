
namespace Ark.Reference.Common.Services.FileStorageService;

public interface IFileStorageService
{
    Task InitAsync(CancellationToken ctk = default);

    Task SaveFileAsync(Guid guid, string filename, Stream fileContent, CancellationToken ctk = default);

    Task GetFileAsync(Stream fileStream, Guid guid, string filename, CancellationToken ctk = default);
}