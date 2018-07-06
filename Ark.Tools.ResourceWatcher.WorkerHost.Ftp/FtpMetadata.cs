using Ark.Tools.FtpClient;
using NodaTime;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public class FtpMetadata : IResourceMetadata
    {
        internal FtpMetadata(FtpEntry entry)
        {
            Entry = entry;
            ResourceId = entry.FullPath;
            Modified = LocalDateTime.FromDateTime(entry.Modified);
        }

        public FtpEntry Entry { get; }

        public LocalDateTime Modified { get; }

        public string ResourceId { get; }

        public object Extensions => new
        {
            Entry.Name,
            Entry.Size,
        };
    }
}
