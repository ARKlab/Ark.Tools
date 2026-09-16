// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

using AwesomeAssertions;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Verifies value equality for incremental interceptor call-site models.</summary>
[TestClass]
public sealed class ToDataTableArkInterceptorModelsTests
{
    /// <summary>Equivalent nested models compare equal and produce the same hash code.</summary>
    [TestMethod]
    public void CallSiteComparer_ComparesNestedModelsByValue()
    {
        var members = ImmutableArray.Create(
            new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"));
        var left = new CallSiteModel(
            new TypeModel("global::Fixture.Entity", "Entity", true, false, members),
            default);
        var right = new CallSiteModel(
            new TypeModel(
                "global::Fixture.Entity",
                "Entity",
                true,
                false,
                ImmutableArray.Create(
                    new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"))),
            default);
        var comparer = (IEqualityComparer<CallSiteModel>)CallSiteModelComparer._instance;

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }
}
