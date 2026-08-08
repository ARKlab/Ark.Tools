// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Reqnroll;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Registers the table mappings used by the sample's contract scenarios.</summary>
[Binding]
public sealed class TableMappingConfiguration
{
    /// <summary>Registers case-sensitive enum conversion and comparison once for the test run.</summary>
    [BeforeTestRun]
    public static void RegisterMappings()
    {
        Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());
    }
}
