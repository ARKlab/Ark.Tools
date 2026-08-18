// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API;
using Ark.MediatorFramework.Sample.Application.Messaging;

[assembly: Ark.MediatorFramework.HttpHost(
    typeof(Book_CreateRequest.V1),
    "/api/v{version}",
    ExcludedContracts = new[]
    {
        typeof(StreamBooksQuery),
        typeof(DescribeBookEditionRequest),
    })]

[assembly: Ark.MediatorFramework.MessagingParticipant(
    Identity = "ark-mediator-functions",
    Network = typeof(BookMessagingNetwork))]
