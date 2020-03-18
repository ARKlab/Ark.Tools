// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
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

namespace Ark.Tools.FtpClient.FtpRequest
{


    internal static class Extensions
    {
        public static async Task<WebResponse> GetResponseAsync(this WebRequest request, CancellationToken ct)
        {
            using (ct.Register(() => request.Abort(), useSynchronizationContext: false))
            {
                try
                {
                    return await request.GetResponseAsync().ConfigureAwait(false);
                }
                catch (WebException ex)
                {
                    // WebException is thrown when request.Abort() is called,
                    // but there may be many other reasons,
                    // propagate the WebException to the caller correctly
                    if (ct.IsCancellationRequested)
                    {
                        if (ex.Response != null)
                            ex.Response.Dispose();
                        // the WebException will be available as Exception.InnerException
                        throw new OperationCanceledException(ex.Message, ex, ct);
                    }

                    // cancellation hasn't been requested, rethrow the original WebException
                    throw;
                }
            }
        }

        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            return task.IsCompleted // fast-path optimization
                ? task
                : task.ContinueWith(
                    completedTask => completedTask.GetAwaiter().GetResult(),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
    }

    internal enum ListStyle
    {
        Unix,
        Windows
    }

    public class FtpClientFactory : IFtpClientFactory
    {
        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FtpClient(host, credentials);
        }
    }

    public class FtpClient : FtpClientBase  //former KailFtpClient ;-)
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private Regex _parseListUnix = new Regex(@"^\s*(?<dir>[-dl])(?<ownerSec>[-r][-w][-x])(?<groupSec>[-r][-w][-x])(?<everyoneSec>[-r][-w][-x])\s+(?:\d+)\s+(?<owner>\w+)\s+(?<group>\w+)\s+(?<size>\d+)\s+(?<modify>\w+\s+\d+\s+\d+:\d+|\w+\s+\d+\s+\d+)\s+(?<name>.*)$", RegexOptions.IgnoreCase);

        private Regex _parseListWindows = new Regex(@"^\s*(?<modify>\d+-\d+-\d+\s+\d+:\d+\w+)\s+(?<dir>[<]dir[>])?\s+(?<size>\d+)?\s+(?<name>.*)$", RegexOptions.IgnoreCase);

        public FtpClient(string host, NetworkCredential credential) : base(host, credential)
        {
        }


        /// <summary>
        /// List all entries of a folder.
        /// </summary>
        /// <param name="path">The folder path to list</param>
        /// <param name="ctk"></param>
        /// <returns>
        /// All entries found (files, folders, symlinks)
        /// </returns>
        public override async Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default(CancellationToken))
        {
            await Task.Yield();

            var ftpPath = path.GetFtpPath();
            _logger.Trace("Listing directory: {0} -> {1}", path, ftpPath);

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {

                var result = new List<FtpEntry>();

                var rq = _createFtpRequest(ftpPath);
                try
                {
                    rq.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                    using (var rs = (FtpWebResponse)(rq.GetResponse()))
                    using (var istrm = rs.GetResponseStream())
                    using (var sr = new StreamReader(istrm))
                    {
                        _logger.Trace("Starting parsing response for path {0}", path);
                        string line = null;
                        while (!linked.Token.IsCancellationRequested && (line = sr.ReadLine()) != null)
                        {
                            result.Add(_parse(line, ftpPath));
                        }
                    }

                    linked.Token.ThrowIfCancellationRequested();

                    return result;
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                        ex.Response.Dispose();
                    throw;
                }
                finally
                {
                    rq.Abort();
                }
            }
        }


        /// <summary>
        /// Downloads the file asynchronous.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="ctk">The CTK.</param>
        /// <returns></returns>
        public override async Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default(CancellationToken))
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {
                var rq = _createFtpRequest(path);
                try
                {
                    rq.Method = WebRequestMethods.Ftp.DownloadFile;
                    rq.UseBinary = true;
                    using (var rs = (FtpWebResponse)(await rq.GetResponseAsync(linked.Token).ConfigureAwait(false)))
                    using (var istrm = rs.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        await istrm.CopyToAsync(ms);
                        return ms.ToArray();
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                        ex.Response.Dispose();
                    throw;
                }
                finally
                {
                    rq.Abort();
                }
            }
        }

        private FtpWebRequest _createFtpRequest(string path)
        {
            var uri = string.Format("ftp://{0}/{1}", this.Host, path);
            _logger.Trace("Creating Ftp Request for path {0} -> {1}", path, uri);

            var rq = (FtpWebRequest)FtpWebRequest.Create(uri);
            rq.Credentials = this.Credentials;
            rq.KeepAlive = true;
            rq.UsePassive = true;
            rq.Timeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;
            rq.ConnectionGroupName = "Ark.Tools.FtpClient";
            rq.ServicePoint.ConnectionLimit = 5;
            rq.ServicePoint.Expect100Continue = false;
            rq.ServicePoint.UseNagleAlgorithm = false;
            return rq;
        }

        #region private helpers
        private FtpEntry _parse(string line, string path)
        {

            Match match = _parseListUnix.Match(line);

            if (match.Success)
                return _parseMatch(match.Groups, ListStyle.Unix, path);

            match = _parseListWindows.Match(line);

            if (match.Success)
                return _parseMatch(match.Groups, ListStyle.Unix, path);


            throw new Exception("Invalid line format: " + line);
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

        public override async Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ctk, cts.Token))
            {
                var rq = _createFtpRequest(path);
                try
                {
                    rq.Method = WebRequestMethods.Ftp.UploadFile;
                    rq.UseBinary = true;
                    rq.ContentLength = content.Length;

                    using (var ostrm = await rq.GetRequestStreamAsync())
                    {
                        await ostrm.WriteAsync(content, 0, content.Length, linked.Token);
                        await ostrm.FlushAsync(linked.Token);
                    }

                    using (var rs = rq.GetResponseAsync(linked.Token))
                    {
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                        ex.Response.Dispose();
                    throw;
                }
                finally
                {
                    rq.Abort();
                }
            }
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
