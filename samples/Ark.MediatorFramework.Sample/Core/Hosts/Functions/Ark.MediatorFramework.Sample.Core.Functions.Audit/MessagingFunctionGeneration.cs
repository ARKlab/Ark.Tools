// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Messages;

[assembly: Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHost(
    typeof(SampleMessagingAuditParticipant),
    Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.ServiceBus,
    ConnectionConfigurationKey = "AzureServiceBus:ConnectionString",
    IncomingSteps = new[] { typeof(Ark.Tools.MediatorFramework.Messaging.UserContextIncomingStep) },
    OutgoingSteps = new[] { typeof(Ark.Tools.MediatorFramework.Messaging.UserContextOutgoingStep) })]
