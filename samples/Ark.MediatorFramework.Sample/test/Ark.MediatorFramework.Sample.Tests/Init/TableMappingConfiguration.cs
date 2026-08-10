// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Reqnroll;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Init;

/// <summary>Composes the Reqnroll table mappings used by application scenarios.</summary>
[Binding]
public sealed class TableMappingConfiguration
{
    /// <summary>Registers the table retrievers and comparers used by the sample.</summary>
    [BeforeTestRun]
    public static void RegisterMappings()
    {
        Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueRetrievers.Register(new EvolvableEnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EvolvableEnumValueRetrieverAndComparer());
    }
}
