using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{
    public interface IFtpConnection : IDisposable
    {
        string Host { get; }
        NetworkCredential Credentials { get; }

        Task ConnectAsync(CancellationToken ctk = default);

        Task DisconnectAsync(CancellationToken ctk = default);

        /* Value */ Task<bool> IsConnected(CancellationToken ctk = default);

        /// <summary>
        /// Download a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <param name="ctk"></param>
        /// <returns>The byte[] of the contents of the file.</returns>
        Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);

        /// <summary>
        /// Upload a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <param name="content">The file contents</param>
        /// <param name="ctk"></param>
        Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);


        /// <summary>
        /// List all entries of a folder. 
        /// </summary>
        /// <param name="path">The folder path to list</param>
        /// <param name="ctk"></param>
        /// <returns>All entries found (files, folders, symlinks)</returns>
        Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = null, CancellationToken ctk = default);
    }
}
