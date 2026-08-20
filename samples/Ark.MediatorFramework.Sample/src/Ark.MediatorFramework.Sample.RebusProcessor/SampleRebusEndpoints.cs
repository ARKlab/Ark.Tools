// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generated;
using Rebus.Config;
using Rebus.Routing;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.RebusProcessor;

/// <summary>Exposes the processor assembly's generated Rebus composition to the API host.</summary>
public static class SampleRebusEndpoints
{
    /// <summary>Registers generated Rebus handlers into a processor container.</summary>
    /// <param name="container">The processor container.</param>
    public static void RegisterHandlers(Container container)
    {
        ArkGeneratedEndpoints.RegisterArkRebusHandlersFromAssembly<ProcessBookPrintProcessRequest>(container);
    }

    /// <summary>Configures generated owner routing.</summary>
    /// <param name="routing">The Rebus router configuration.</param>
    public static void ConfigureRouting(StandardConfigurer<IRouter> routing)
    {
        ArkGeneratedEndpoints.ConfigureArkRebusRouting<ProcessBookPrintProcessRequest>(routing);
    }
}
