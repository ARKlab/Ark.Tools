using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

namespace TestWorker.Dto
{
    public class Test_File : IResource<Test_FileMetadataDto>
    {
        public Test_File(Test_FileMetadataDto metadata)
        {
            Metadata = metadata;
        }
        public Test_FileMetadataDto Metadata { get; protected set; }
        public Instant DownloadedAt { get; set; }
        public byte[]? RawData { get; set; }

        public string? CheckSum { get; internal set; }

        Instant IResourceState.RetrievedAt => DownloadedAt;
    }

}