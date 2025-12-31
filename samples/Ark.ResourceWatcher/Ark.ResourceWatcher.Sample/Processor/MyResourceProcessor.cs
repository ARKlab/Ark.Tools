// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Globalization;

using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Transform;
using Ark.Tools.Http;
using Ark.Tools.ResourceWatcher.WorkerHost;

using Flurl.Http;

using NLog;

namespace Ark.ResourceWatcher.Sample.Processor
{
    /// <summary>
    /// Processor that transforms blob content and sends it to a sink API.
    /// </summary>
    public sealed class MyResourceProcessor : IResourceProcessor<MyResource, MyMetadata>
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly IFlurlClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="MyResourceProcessor"/> class.
        /// </summary>
        /// <param name="clientFactory">The Flurl client factory.</param>
        /// <param name="config"></param>
        public MyResourceProcessor(IArkFlurlClientFactory clientFactory, IMyResourceProcessorConfig config)
        {
            _client = clientFactory.Get(config.SinkUrl);
        }

        /// <inheritdoc/>
        public async Task Process(MyResource file, CancellationToken ctk = default)
        {
            _logger.Info(CultureInfo.InvariantCulture, "Processing blob {ResourceId} ({Size} bytes)",
                file.Metadata.ResourceId, file.Data.Length);

            // Transform the CSV content
            var transformer = new MyTransformService(file.Metadata.ResourceId);
            var sinkData = transformer.Transform(file.Data);

            _logger.Debug(CultureInfo.InvariantCulture, "Transformed {RecordCount} records from {ResourceId}",
                sinkData.Records.Count, file.Metadata.ResourceId);

            // Send to sink API
            await _client
                .Request("data")
                .PostJsonAsync(sinkData, cancellationToken: ctk);

            _logger.Info(CultureInfo.InvariantCulture, "Successfully processed blob {ResourceId}",
                file.Metadata.ResourceId);
        }
    }

    public interface IMyResourceProcessorConfig
    {
        Uri SinkUrl { get; }
    }
}
