using System;
using System.Net;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public interface IFtpConfig
    {
        string Host { get; }
        NetworkCredential Credentials { get; }
        TimeSpan ListingTimeout { get; }
        TimeSpan DownloadTimeout { get; }
    }
}
