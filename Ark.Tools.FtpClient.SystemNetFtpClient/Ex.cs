// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using Polly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace System.Net.FtpClient
{
    public static class Ex
    {
        public static IEnumerable<FtpListItem> GetFileListingRecursive(this FtpClient client, string startPath, FtpListOption options)
        {
            Policy retrier = Policy
              .Handle<Exception>()
              .WaitAndRetry(new[]
              {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15)
              });

            if (options.HasFlag(FtpListOption.Recursive))
                throw new ArgumentException("Do not use recursive option when doing a recursive listing.", "options");

            var foldersToGet = new Stack<string>();
            foldersToGet.Push(startPath);

            IEnumerable<FtpListItem> res = new FtpListItem[0];

            do
            {

                var path = foldersToGet.Pop();
                var files = retrier.Execute(() =>
                {
                    var result = client.GetListing(path, options);
                    return result;
                });

                foreach (var d in files.Where(x => x.Type == FtpFileSystemObjectType.Directory))
                    foldersToGet.Push(d.FullName);

                res = res.Concat(files.Where(x => x.Type != FtpFileSystemObjectType.Directory).ToList());


            } while (foldersToGet.Count > 0);

            return res;
        }

        public static IEnumerable<FtpListItem> GetFileListingRecursiveParallel(this FtpClient client, string startPath, FtpListOption options)
        {
            Policy retrier = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
              {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15)
              });

            if (options.HasFlag(FtpListOption.Recursive))
                throw new ArgumentException("Do not use recursive option when doing a recursive listing.", "options");

            List<Task<FtpListItem[]>> pending = new List<Task<FtpListItem[]>>();
            IEnumerable<FtpListItem> files = new List<FtpListItem>();

            Func<string, Task<FtpListItem[]>> listFolderAsync = (string path) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    return retrier.Execute(() =>
                    {
                        return client.GetListing(path, options);
                    });

                });

            };

            pending.Add(listFolderAsync(startPath));

            int completedTaskIndex;
            while ((completedTaskIndex = Task.WaitAny(pending.ToArray())) != -1 && pending.Count > 0)
            {
                var t = pending.ElementAt(completedTaskIndex);
                pending.RemoveAt(completedTaskIndex);
                var list = t.Result;

                foreach (var d in list.Where(x => x.Type == FtpFileSystemObjectType.Directory))
                    pending.Add(listFolderAsync(d.FullName));

                files = files.Concat(list.Where(x => x.Type != FtpFileSystemObjectType.Directory).ToList());
            }

            return files;
        }

        public static byte[] DownloadFile(this FtpClient client, FtpListItem ftpListItem, bool checkModify = false)
        {
            Policy retrier = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
              {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15)
              });

            Ensure.Bool.IsTrue(checkModify && ftpListItem.Modified != default(DateTime), "Modified field of ftpListItem was not populated. CheckModify cannot be performed. Set checkModify to false.");
            // download the file - RAW
            return retrier.Execute(() =>
            {
                byte[] rawData = null;
                using (var inputStream = client.OpenRead(ftpListItem.FullName))
                using (var mem = new MemoryStream())
                {
                    inputStream.CopyTo(mem);
                    rawData = mem.ToArray();
                }
                var afterDownload = client.GetModifiedTime(ftpListItem.FullName);
                if (ftpListItem.Modified != afterDownload)
                {
                    throw new InvalidDataException("Downloaded data changed during download - unknown version, please retry.");
                }
                return rawData;
            });

            
        }


    }
}
