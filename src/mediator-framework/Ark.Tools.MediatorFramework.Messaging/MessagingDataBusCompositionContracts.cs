// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Messaging;

internal interface IMessagingDataBusAttachmentLifetime
{
    TimeSpan MinimumAttachmentLifetime { get; }
}

internal interface IMessagingDataBusStartupValidation
{
    Task ValidateAsync(CancellationToken cancellationToken);
}

internal interface IMessagingDataBusHostedServiceRegistration
{
    void RegisterServices(IServiceCollection services);
}
