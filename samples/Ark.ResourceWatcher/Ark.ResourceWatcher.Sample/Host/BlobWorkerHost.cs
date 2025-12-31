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

namespace Ark.ResourceWatcher.Sample.Host
{
    /// <summary>
    /// Worker host for processing blobs from external storage.
    /// </summary>
    public sealed class BlobWorkerHost : WorkerHost<BlobResource, BlobMetadata, BlobQueryFilter>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlobWorkerHost"/> class.
        /// </summary>
        /// <param name="config">The worker host configuration.</param>
        public BlobWorkerHost(BlobWorkerHostConfig config)
            : base(config)
        {
            Use(d =>
            {
                // commond deps
                d.Container.RegisterInstance<IArkFlurlClientFactory>(new ArkFlurlClientFactory());
                d.Container.RegisterInstance<IClock>(SystemClock.Instance);
            });

            UseDataProvider<BlobStorageResourceProvider>(d =>
            {
                d.Container.RegisterInstance<IBlobStorageResourceProviderConfig>(config);
            });

            AppendFileProcessor<BlobResourceProcessor>(d =>
            {
                d.Container.RegisterInstance<IBlobResourceProcessorConfig>(config);
            });

            UseStateProvider<InMemStateProvider>();
        }
    }
}
