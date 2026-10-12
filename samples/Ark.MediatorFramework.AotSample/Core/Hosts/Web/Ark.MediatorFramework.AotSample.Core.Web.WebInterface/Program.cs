// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

var builder = WebInterfaceApp.CreateBuilder(args);
var app = builder.Build();
await app.Services.GetRequiredService<AotDataContextFactory>().EnsureCreatedAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
WebInterfaceApp.Configure(app);
await app.RunAsync().ConfigureAwait(false);
