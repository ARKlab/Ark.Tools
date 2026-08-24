// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Messaging;

internal sealed class AzureBlobMessagingDataBusStartupValidator : IHostedService
{
    private readonly AzureBlobMessagingDataBus _dataBus;

    public AzureBlobMessagingDataBusStartupValidator(AzureBlobMessagingDataBus dataBus)
    {
        _dataBus = dataBus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _dataBus.ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
