// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Verifies value equality for incremental interceptor call-site models.</summary>
[TestClass]
public sealed class ToDataTableArkInterceptorModelsTests
{
    /// <summary>Equivalent nested models compare equal and produce the same hash code.</summary>
    [TestMethod]
    public async Task CallSiteComparer_ComparesNestedModelsByValue()
    {
        var members = ImmutableArray.Create(
            new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"));
        var location = await _locationAsync().ConfigureAwait(false);
        var left = new CallSiteModel(
            new TypeModel("global::Fixture.Entity", "Entity", true, false, members),
            location);
        var right = new CallSiteModel(
            new TypeModel(
                "global::Fixture.Entity",
                "Entity",
                true,
                false,
                ImmutableArray.Create(
                    new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"))),
            location);
        var comparer = (System.Collections.Generic.IEqualityComparer<CallSiteModel>)CallSiteModelComparer._instance;

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    private static async Task<InterceptableLocation> _locationAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class Fixture { void Caller() { Target(); } void Target() { } }");
        var compilation = CSharpCompilation.Create(
            "ComparerTest",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        return model.GetInterceptableLocation(invocation, CancellationToken.None)!;
    }
}
