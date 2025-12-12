using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.FileStorageService
{
    public interface IFileStorageService
    {
        Task InitAsync();

        Task SaveFileAsync(Guid guid, string filename, Stream fileContent, CancellationToken ctk = default);

        Task GetFileAsync(Stream fileStream, Guid guid, string filename, CancellationToken ctk = default);
    }
}
