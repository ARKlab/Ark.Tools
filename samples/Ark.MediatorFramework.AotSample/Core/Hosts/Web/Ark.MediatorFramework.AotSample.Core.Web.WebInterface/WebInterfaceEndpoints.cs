// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.Tools.MediatorFramework.MinimalApi;

namespace Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

/// <summary>Selects the API contracts for the generated Minimal API endpoints.</summary>
[ArkGenerateMinimalApiForAssembly(typeof(Book))]
public partial class WebInterfaceEndpoints;
