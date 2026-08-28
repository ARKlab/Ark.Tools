// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.Messages;

[assembly: Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHost(
    typeof(SampleMessagingNotificationParticipant),
    Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.ServiceBus,
    ConnectionConfigurationKey = "AzureServiceBus:ConnectionString")]
