// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.Web.Hosting;
using Ark.Tools.Compliance;

using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddArkRedaction();
WebHosting.AddProcessor(builder.Services, builder.Configuration);
await builder.Build().RunAsync().ConfigureAwait(false);
