// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{

    public interface IFtpClient
    {
        Uri Uri { get; }
        NetworkCredential? Credentials { get; }

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
        Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default);

        /// <summary>
        /// List a directory recursively and returns the files found. 
        /// </summary>
        /// <param name="startPath">The directory to list recursively</param>
        /// <param name="skipFolder">Predicate returns true for folders that are to be skipped.</param>
        /// <param name="ctk"></param>
        /// <returns>The files found.</returns>
        Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default);
    }
}