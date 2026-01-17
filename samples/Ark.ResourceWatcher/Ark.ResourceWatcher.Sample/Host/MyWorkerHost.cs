// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Config;
using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Processor;
using Ark.ResourceWatcher.Sample.Provider;

using Ark.Tools.Http;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

namespace Ark.ResourceWatcher.Sample.Host;

/// <summary>
/// Worker host for processing blobs from external storage.
/// Uses strongly-typed <see cref="BlobExtensions"/> for incremental loading support.
/// </summary>
public sealed class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, BlobExtensions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyWorkerHost"/> class.
    /// </summary>
    /// <param name="config">The worker host configuration.</param>
    public MyWorkerHost(MyWorkerHostConfig config)
        : base(config)
    {
        Use(d =>
        {
            // commond deps
            d.Container.RegisterInstance<IArkFlurlClientFactory>(new ArkFlurlClientFactory());
            d.Container.RegisterInstance<IClock>(SystemClock.Instance);
        });

        UseDataProvider<MyStorageResourceProvider>(d =>
        {
            d.Container.RegisterInstance<IMyStorageResourceProviderConfig>(config);
        });

        AppendFileProcessor<MyResourceProcessor>(d =>
        {
            d.Container.RegisterInstance<IMyResourceProcessorConfig>(config);
        });

        UseStateProvider<InMemStateProvider<BlobExtensions>>();
    }
}