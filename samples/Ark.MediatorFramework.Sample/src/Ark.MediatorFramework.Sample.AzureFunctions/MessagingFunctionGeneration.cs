// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.Messages;

[assembly: Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHost(
    typeof(SampleMessagingParticipant),
    Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.StorageQueue,
    ConnectionConfigurationKey = "AzureWebJobsStorage")]
