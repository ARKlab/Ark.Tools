// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Reqnroll;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.Tools.ResourceWatcher.Tests.Init;

[Binding]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Reqnroll requires instance methods for BeforeTestRun")]
public class TableMappingConfiguration
{
    [BeforeTestRun]
    public static void SupportTableWithNodaTime()
    {
        // Last takes priority, this is a last resort
        Service.Instance.ValueRetrievers.Register(new StringTypeConverterValueRetriver());

        // Register NodaTime support for DataTable conversions
        Service.Instance.ValueRetrievers.Register(new NodaTimeValueRetriverAndComparer());
        Service.Instance.ValueComparers.Register(new NodaTimeValueRetriverAndComparer());

        // Register Enum support
        Service.Instance.ValueRetrievers.Register(new EnumValueRetrieverAndComparer());
        Service.Instance.ValueComparers.Register(new EnumValueRetrieverAndComparer());

        // Register String support
        Service.Instance.ValueRetrievers.Register(new StringValueRetriverAndComparer());
        Service.Instance.ValueComparers.Register(new StringValueRetriverAndComparer());
    }
}