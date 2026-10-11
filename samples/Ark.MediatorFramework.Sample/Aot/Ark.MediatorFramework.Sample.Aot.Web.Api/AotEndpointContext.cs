// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Aot.Application;
using Ark.Tools.MediatorFramework.MinimalApi;

namespace Ark.MediatorFramework.Sample.Aot.Web.Api;

/// <summary>Selects the Application contracts for the generated Minimal API endpoints.</summary>
[ArkGenerateMinimalApiForAssembly(typeof(Greeting))]
public partial class AotEndpointContext;
