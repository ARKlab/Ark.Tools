// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API;

[assembly: Ark.MediatorFramework.HttpHost(
    typeof(Book_CreateRequest.V1),
    "/api/v{version}",
    ExcludedContracts = new[]
    {
        typeof(DescribeShapeRequest),
        typeof(StreamBooksQuery),
        typeof(DescribeBookEditionRequest),
    })]
