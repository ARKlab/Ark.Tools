// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AzureFunctions;

using Ark.MediatorFramework.Sample.Application;

[assembly: Ark.MediatorFramework.HttpHost(
    typeof(ApplicationComposition),
    "/api/v{version}",
    ExcludedContracts = new[]
    {
        typeof(Greeting_CreateRequest.V1),
        typeof(DescribeShapeRequest),
    })]
