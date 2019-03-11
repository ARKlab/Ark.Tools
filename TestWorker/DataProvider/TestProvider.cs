using NLog;
using NodaTime;
using System;
using Ark.Tools.ResourceWatcher.WorkerHost;
using TestWorker.Dto;
using Ark.Tools.ResourceWatcher;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace TestWorker.DataProvider
{
    public class Test_ProviderFilter
    {
        public LocalDateTime Date { get; set; }
    }

    public class TestProvider : IResourceProvider<Test_FileMetadataDto, Test_File, Test_ProviderFilter>
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public TestProvider()
        {
        }

        public Task<IEnumerable<Test_FileMetadataDto>> GetMetadata(Test_ProviderFilter filter, CancellationToken ctk = default)
        {
            return Task.Run(() =>
            {
                var metadataList = new List<Test_FileMetadataDto>();

                metadataList.Add(new Test_FileMetadataDto
                {
                    FileName = "TestFileName",
                });

                return metadataList.AsEnumerable();
            });
        }

        public Task<Test_File> GetResource(Test_FileMetadataDto metadata, IResourceTrackedState lastState, CancellationToken ctk = default)
        {
            return Task.Run(() =>
            {
                var downloadedFile = new Test_File(metadata)
                {
                    DownloadedAt = SystemClock.Instance.GetCurrentInstant(),
                };

                _logger.Info($"File {downloadedFile.Metadata.FileName} downloaded successfully");

                return downloadedFile;
            });
        }
    }
}
