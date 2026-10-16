// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

using NLog;

using Polly;
using Polly.Retry;

namespace Ark.Tools.FtpClient.Core;


public abstract class FtpClientBase : IFtpClient
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public Uri Uri { get; }

    [Secret]
    public NetworkCredential Credentials { get; }

    public FtpConfig FtpConfig { get; }

    protected FtpClientBase(FtpConfig ftpConfig)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        Uri = ftpConfig.Uri;
        Credentials = ftpConfig.Credentials;

        FtpConfig = ftpConfig;
    }

    public abstract Task<byte[]> DownloadFileAsync(string path, CancellationToken ctk = default);
    public abstract Task<IEnumerable<FtpEntry>> ListDirectoryAsync(string path = "./", CancellationToken ctk = default);
    public abstract Task UploadFileAsync(string path, byte[] content, CancellationToken ctk = default);

    /// <summary>
    /// Lists the files under <paramref name="startPath"/> recursively, one folder at a time through <see cref="ListDirectoryAsync"/>.
    /// </summary>
    /// <param name="startPath">The folder to start from.</param>
    /// <param name="skipFolder">Returns true for the folders that must not be listed.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The files found.</returns>
    public virtual async Task<IEnumerable<FtpEntry>> ListFilesRecursiveAsync(string startPath = "./", Predicate<FtpEntry>? skipFolder = null, CancellationToken ctk = default)
    {
        _logger.Trace(CultureInfo.InvariantCulture, "List files starting from path: {Path}", startPath);
        startPath ??= "./";

        Stack<string> pendingFolders = new();
        pendingFolders.Push(startPath);
        List<FtpEntry> files = new();

        while (pendingFolders.TryPop(out var path))
        {
            var list = await _listFolderWithRetryAsync(path, ctk).ConfigureAwait(false);

            foreach (var d in _foldersToList(list, skipFolder))
                pendingFolders.Push(d.FullPath);

            files.AddRange(list.Where(static x => !x.IsDirectory));
        }

        return files;
    }

    // Retries a failed folder listing once, as both the sequential and the pool walk do.
    private protected async Task<IEnumerable<FtpEntry>> _listFolderWithRetryAsync(string path, CancellationToken ctk)
    {
        var retrier = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 1,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = args =>
                {
                    _logger.Warn(args.Outcome.Exception, CultureInfo.InvariantCulture, "Failed to list folder {Path}. Try again in {Sleep} ...", path, args.RetryDelay);
                    return default;
                },
            })
            .Build();

        return await retrier.ExecuteAsync(async ct1 =>
        {
            return await ListDirectoryAsync(path, ct1).ConfigureAwait(false);
        }, ctk).ConfigureAwait(false);
    }

    // The sub folders of a listing to walk next, without '.', '..' and the folders skipFolder rejects.
    private protected static IEnumerable<FtpEntry> _foldersToList(IEnumerable<FtpEntry> list, Predicate<FtpEntry>? skipFolder)
    {
        foreach (var d in list.Where(static x => x.IsDirectory && !x.Name.Equals(".", StringComparison.Ordinal) && !x.Name.Equals("..", StringComparison.Ordinal)))
        {
            if (skipFolder?.Invoke(d) == true)
                _logger.Info(CultureInfo.InvariantCulture, "Skipping folder: {Path}", d.FullPath);
            else
                yield return d;
        }
    }

    public abstract Task DeleteFileAsync(string path, CancellationToken ctk = default);
    public abstract Task DeleteDirectoryAsync(string path, CancellationToken ctk = default);
}