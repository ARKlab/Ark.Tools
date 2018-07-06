using NodaTime;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public sealed class FtpFile<TPayload> : IResource<FtpMetadata>
    {
        public FtpFile(FtpMetadata metadata)
        {
            Metadata = metadata;
        }

        public FtpMetadata Metadata { get; }

        public Instant RetrievedAt { get; internal set; }

        public string CheckSum { get; internal set; }

        public TPayload ParsedData { get; internal set; }
    }
}
