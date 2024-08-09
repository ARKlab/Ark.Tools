using Azure.Identity;
using Azure.Storage.Blobs;

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.FileStorageService
{
    public class FileStorageService
        : IFileStorageService
    {
        private readonly IFileStorageServiceConfig _config;
        private readonly BlobContainerClient _fileClient;

        public FileStorageService(IFileStorageServiceConfig config)
        {
            _config = config;

            // there is no straigh support for switching between AccountKey and MSI supported by Blob library
            var client = new BlobServiceClient(_config.StorageAccount);

            // in case of Development or Local
            if (client.Uri.Scheme == "http")
            { }
            // in case credentials are supplied
            else if (_config.StorageAccount.Contains("AccountKey") || _config.StorageAccount.Contains("SharedAccessSignature"))
            { }
            else // anon, assume MSI
            {
                client = new BlobServiceClient(client.Uri, new DefaultAzureCredential());
            }

            _fileClient = client.GetBlobContainerClient(_config.StoragePrefix.ToLowerInvariant());
        }

        public Task SaveFileAsync(Guid guid, string filename, Stream fileContent, CancellationToken ctk)
        {
            var blob = _getBlobFor(guid, filename);

            return blob.UploadAsync(fileContent, ctk);
        }

        public async Task GetFileAsync(Stream fileStream, Guid guid, string filename, CancellationToken ctk)
        {
            var blob = _getBlobFor(guid, filename);

            await blob.DownloadToAsync(fileStream, cancellationToken: ctk);

            fileStream.Seek(0, SeekOrigin.Begin);
        }

        private BlobClient _getBlobFor(Guid guid, string filename)
        {
            var name = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", guid.ToString(), filename);
            return _fileClient
                .GetBlobClient(name);
        }

        public async Task InitAsync()
        {
            await _fileClient.CreateIfNotExistsAsync();
        }
    }
}
