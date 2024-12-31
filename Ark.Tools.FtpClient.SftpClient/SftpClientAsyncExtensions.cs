﻿using Renci.SshNet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Client = Renci.SshNet.SftpClient;

namespace Ark.Tools.FtpClient.SftpClient
{
    public static class SftpClientAsyncExtensions
    {
        /// <summary>
        /// Asynchronously download the file into the stream.
        /// </summary>
        /// <param name="client">The <see cref="Client"/> instance</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="output">Data output stream.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns></returns>
        public static Task DownloadAsync(this Client client,
            string path, Stream output,
            TaskFactory? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task.Factory).FromAsync(
                client.BeginDownloadFile(path, output),
                client.EndDownloadFile,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously download the file into the stream.
        /// </summary>
        /// <param name="client">The <see cref="Client"/> instance</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="output">Data output stream.</param>
        /// <param name="downloadCallback">The download callback.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns></returns>
        public static Task DownloadAsync(this Client client,
            string path, Stream output, Action<ulong> downloadCallback,
            TaskFactory? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task.Factory).FromAsync(
                client.BeginDownloadFile(path, output, null, null, downloadCallback),
                client.EndDownloadFile,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously upload the stream into the remote file.
        /// </summary>
        /// <param name="client">The <see cref="Client"/> instance</param>
        /// <param name="input">Data input stream.</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="uploadCallback">The upload callback.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns></returns>
        public static Task UploadAsync(this Client client,
            Stream input, string path, Action<ulong>? uploadCallback = null,
            TaskFactory? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task.Factory).FromAsync(
                client.BeginUploadFile(input, path, null, null, uploadCallback),
                client.EndUploadFile,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously upload the stream into the remote file.
        /// </summary>
        /// <param name="client">The <see cref="Client"/> instance</param>
        /// <param name="input">Data input stream.</param>
        /// <param name="path">Remote file path.</param>
        /// <param name="canOverride">if set to <c>true</c> then existing file will be overwritten.</param>
        /// <param name="uploadCallback">The upload callback.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns></returns>
        public static Task UploadAsync(this Client client,
            Stream input, string path, bool canOverride, Action<ulong>? uploadCallback = null,
            TaskFactory? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task.Factory).FromAsync(
                client.BeginUploadFile(input, path, canOverride, null, null, uploadCallback),
                client.EndUploadFile,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously synchronizes the directories.
        /// </summary>
        /// <param name="client">The <see cref="Client"/> instance</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="searchPattern">The search pattern.</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>List of uploaded files.</returns>
        public static Task<IEnumerable<FileInfo>> SynchronizeDirectoriesAsync(this Client client,
            string sourcePath, string destinationPath, string searchPattern,
            TaskFactory<IEnumerable<FileInfo>>? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task<IEnumerable<FileInfo>>.Factory).FromAsync(
                client.BeginSynchronizeDirectories(sourcePath, destinationPath, searchPattern, null, null),
                client.EndSynchronizeDirectories,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously run a command.
        /// </summary>
        /// <param name="command">The <see cref="SshCommand"/> instance</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Command execution result.</returns>
        public static Task<string> ExecuteAsync(this SshCommand command,
            TaskFactory<string>? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task<string>.Factory).FromAsync(
                command.BeginExecute(),
                command.EndExecute,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }

        /// <summary>
        /// Asynchronously run a command.
        /// </summary>
        /// <param name="command">The <see cref="SshCommand"/> instance</param>
        /// <param name="commandText">The command text to execute</param>
        /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
        /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
        /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
        /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// that is used to schedule the task that executes the end method.</param>
        /// <returns>Command execution result.</returns>
        public static Task<string> ExecuteAsync(this SshCommand command,
            string commandText,
            TaskFactory<string>? factory = null,
            TaskCreationOptions creationOptions = default(TaskCreationOptions),
            TaskScheduler? scheduler = null)
        {
            return (factory = factory ?? Task<string>.Factory).FromAsync(
                command.BeginExecute(commandText, null, null),
                command.EndExecute,
                creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);
        }
    }
}