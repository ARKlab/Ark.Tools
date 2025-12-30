// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Config;
using Ark.ResourceWatcher.Sample.Host;

using Ark.Tools.NLog;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.ResourceWatcher.Sample;

sealed class Program
{
    static void Main(string[] args)
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .AddWorkerHostInfrastracture()
            .ConfigureNLog("BlobWorkerSample")
            .AddWorkerHost(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();

                var config = new BlobWorkerHostConfig
                {
                    WorkerName = "BlobWorkerSample",
                    Sleep = TimeSpan.FromMinutes(1),
                    MaxRetries = 3,
                    DegreeOfParallelism = 2
                };

                var blobStorageUrl = new Uri(cfg["BlobStorage:BaseUrl"] ?? "http://localhost:5000");
                var sinkUrl = new Uri(cfg["Sink:BaseUrl"] ?? "http://localhost:5001");

                return new BlobWorkerHost(config, blobStorageUrl, sinkUrl);
            })
            .UseConsoleLifetime();

        hostBuilder.StartAndWaitForShutdown();
    }
}
