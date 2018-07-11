// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using Polly;
using System;
using System.Collections.Generic;

using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Async;
using EnsureThat;
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.SftpClient
{
    public class SFtpClientFactory : IFtpClientFactory
    {
        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new SftpClient(host, credentials);
        }
    }

    internal enum ListStyle
    {
        Unix,
        Windows
    }

    public class SftpClient : IFtpClient  //former KailFtpClient ;-)
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private Regex _parseListUnix = new Regex(@"^\s*(?<dir>[-dl])(?<ownerSec>[-r][-w][-x])(?<groupSec>[-r][-w][-x])(?<everyoneSec>[-r][-w][-x])\s+(?:\d+)\s+(?<owner>\w+)\s+(?<group>\w+)\s+(?<size>\d+)\s+(?<modify>\w+\s+\d+\s+\d+:\d+|\w+\s+\d+\s+\d+)\s+(?<name>.*)$", RegexOptions.IgnoreCase);

        private Regex _parseListWindows = new Regex(@"^\s*(?<modify>\d+-\d+-\d+\s+\d+:\d+\w+)\s+(?<dir>[<]dir[>])?\s+(?<size>\d+)?\s+(?<name>.*)$", RegexOptions.IgnoreCase);

        public SftpClient(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            var r = Regex.Match(host, @":\d+$");
            if (r.Success)
            {
                this.Host = host.Substring(0, r.Index);
                this.Port = Convert.ToInt16(host.Substring(r.Index + 1));
            } else
            {
                this.Host = host;
                this.Port = 2222;
            }

            this.Credentials = credentials;
        }

        public NetworkCredential Credentials { get; }
        public string Host { get; }
        public int Port { get; }

        /// <summary>
        /// List all entries of a folder.
        /// </summary>
        /// <param name="path">The folder path to list</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// All entries found (files, folders, symlinks)
        /// </returns>
        public async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default(CancellationToken))
        {
            await Task.Yield();

            var ftpPath = path.GetFtpPath();
            _logger.Trace("Listing directory: {0} -> {1}", path, ftpPath);

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {

                var result = new List<FtpEntry>();

                using (var sFtpClient = new Renci.SshNet.SftpClient(new ConnectionInfo(Host, Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password))))
                {

                    sFtpClient.Connect();
                    var rawLs = await sFtpClient.ListDirectoryAsync(path);
                    _logger.Trace("Starting parsing response for path {0}", path);
                    result = _parse(rawLs, linked.Token);

                }

                linked.Token.ThrowIfCancellationRequested();

                return result;

            }
        }

        /// <summary>
        /// List a directory recursively and returns the files found.
        /// </summary>
        /// <param name="startPath">The directory to list recursively</param>
        /// <param name="skipFolder">Predicate returns true for folders that are to be skipped.</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// The files found.
        /// </returns>
        public async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry> skipFolder = null, CancellationToken ctk = default(CancellationToken))
        {
            _logger.Trace("List files starting from path: {0}", startPath);

            if (skipFolder == null)
                skipFolder = x => false;

            List<Task<IEnumerable<FtpEntry>>> pending = new List<Task<IEnumerable<FtpEntry>>>();
            IEnumerable<FtpEntry> files = new List<FtpEntry>();

            Func<string, CancellationToken, Task<IEnumerable<FtpEntry>>> listFolderAsync = async (string path, CancellationToken ct) =>
            {

                var retrier = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(new[]
                    {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15)
                    }, (ex, ts) =>
                    {
                        _logger.Warn(ex, "Failed to list folder {0}. Try again soon ...", path);
                    });
                var res = await retrier.ExecuteAsync(async ct1 =>
                {
                    return await this.ListDirectoryAsync(path, ct1).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);
                return res;
            };

            pending.Add(listFolderAsync(startPath, ctk));

            while (pending.Count > 0)
            {
                var completedTask = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(completedTask);

                // task could have completed with errors ... strange, let them progate.
                var list = await completedTask.ConfigureAwait(false);

                //we want to exclude folders . and .. that we dont want to search
                foreach (var d in list.Where(x => x.IsDirectory && !x.Name.Equals(".") && !x.Name.Equals("..")))
                {
                    if (skipFolder.Invoke(d))
                        _logger.Info("Skipping folder: {0}", d.FullPath);
                    else
                        pending.Add(listFolderAsync(d.FullPath, ctk));
                }

                files = files.Concat(list.Where(x => !x.IsDirectory).ToList());
            }

            return files;

        }

        /// <summary>
        /// Downloads the file asynchronous.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="ctk">The CTK.</param>
        /// <returns></returns>
        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {
                using (var sFtpClient = new Renci.SshNet.SftpClient(new ConnectionInfo(Host, Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password))))
                {
                    sFtpClient.Connect();
                    using (var ms = new MemoryStream())
                    {
                        await sFtpClient.DownloadAsync(path, ms, u => { });
                        return ms.ToArray();
                    }

                }
            }
        }

        public async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {
                using (var sFtpClient = new Renci.SshNet.SftpClient(new ConnectionInfo(Host, Port, Credentials.UserName, new PasswordAuthenticationMethod(Credentials.UserName, Credentials.Password))))
                {
                    sFtpClient.Connect();
                    using (var ms = new MemoryStream(content))
                    {
                        await sFtpClient.UploadAsync(ms, path);
                    }
                }
            }
        }

        #region private helpers
        private List<FtpEntry> _parse(IEnumerable<SftpFile> files, CancellationToken ctk)
        {
            var result = new List<FtpEntry>();

            foreach (var file in files)
            {
                var entry = new FtpEntry();
                entry.FullPath = file.FullName;
                entry.IsDirectory = file.IsDirectory;
                entry.Modified = file.LastWriteTimeUtc;
                entry.Name = file.Name;
                entry.Size = file.Length;
                result.Add(entry);
                if (ctk.IsCancellationRequested)
                {
                    return result;
                }
            }
            return result;
        }


        private FtpEntry _parseMatch(GroupCollection matchGroups, ListStyle style, string path)
        {
            string dirMatch = (style == ListStyle.Unix ? "d" : "<dir>");

            FtpEntry result = new FtpEntry();
            result.IsDirectory = matchGroups["dir"].Value.Equals(dirMatch, StringComparison.InvariantCultureIgnoreCase);
            result.Name = matchGroups["name"].Value.GetFtpPath();
            result.FullPath = path.GetFtpPath(result.Name);

            if (matchGroups["modify"].Value.Length > 0)
                result.Modified = matchGroups["modify"].Value.GetFtpDate(DateTimeStyles.AssumeLocal);

            if (!result.IsDirectory)
                result.Size = long.Parse(matchGroups["size"].Value);

            return result;
        }

        

        #endregion private helpers

    }

    internal static class FtpPathExtensions
    {

        public static string GetFtpPath(this string path)
        {
            if (String.IsNullOrEmpty(path))
                return "./";

            path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
            if (path.Length == 0)
                path = "./";

            return path;
        }

        public static string GetFtpPath(this string path, params string[] segments)
        {
            if (String.IsNullOrEmpty(path))
                path = "./";

            foreach (string part in segments)
            {
                if (part != null)
                {
                    if (path.Length > 0 && !path.EndsWith("/"))
                        path += "/";
                    path += Regex.Replace(part.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
                }
            }

            path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
            if (path.Length == 0)
                path = "./";

            /*if (!path.StartsWith("/") || !path.StartsWith("./"))
                path = "./" + path;*/

            return path;
        }

        public static string GetFtpFileName(this string path)
        {
            string tpath = (path == null ? null : path);
            int lastslash = -1;

            if (tpath == null)
                return null;

            lastslash = tpath.LastIndexOf('/');
            if (lastslash < 0)
                return tpath;

            lastslash += 1;
            if (lastslash >= tpath.Length)
                return tpath;

            return tpath.Substring(lastslash, tpath.Length - lastslash);
        }

        public static DateTime GetFtpDate(this string date, DateTimeStyles style)
        {
            string[] formats = new string[] {
                "yyyyMMddHHmmss",
                "yyyyMMddHHmmss.fff",
                "MMM dd  yyyy",
                "MMM  d  yyyy",
                "MMM dd HH:mm",
                "MMM  d HH:mm",
                "MM-dd-yy  hh:mmtt",
                "MM-dd-yyyy  hh:mmtt"
            };
            DateTime parsed;

            if (DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, style, out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }
    }

}
