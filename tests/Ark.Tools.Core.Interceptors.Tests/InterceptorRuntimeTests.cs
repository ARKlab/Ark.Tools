// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Runtime.CompilerServices;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>
/// Proves, end-to-end, that <c>ToDataTableArkInterceptorGenerator</c> actually intercepts
/// compile-time-known <c>ToDataTableArk&lt;T&gt;()</c> calls in a project that wires it (unlike
/// <c>Ark.Tools.Core.Tests</c>, which never references the generator and always uses the
/// reflection fallback), and that ineligible call sites (open type parameters, types with a custom
/// base class) still produce correct results by safely falling back to reflection.
/// </summary>
[TestClass]
public class InterceptorRuntimeTests
{
    /// <summary>
    /// Calling ToDataTableArk() with the compile-time-known, eligible <see cref="InterceptedEntity"/>
    /// produces the exact same schema/values that the reflection-based implementation would.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_WithEligibleType_ProducesCorrectDataTable()
    {
        var entities = new[]
        {
            new InterceptedEntity
            {
                Id = 1,
                Name = "Widget",
                Measurement = 3.5,
                Amount = 9.99m,
                IsEnabled = true,
                OptionalCount = 4,
                CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = InterceptedStatus.Active,
                CorrelationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                EffectiveDate = new NodaTime.LocalDate(2024, 6, 1),
            },
        };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(10);
        table.Columns["Status"]!.DataType.Should().Be<string>();
        table.Rows[0]["Id"].Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Widget");
        table.Rows[0]["Status"].Should().Be("Active");
        table.Rows[0]["EffectiveDate"].Should().Be(entities[0].EffectiveDate.ToDateTimeUnspecified());
    }

    /// <summary>
    /// A second, distinct call site using the same T as the previous test proves the generator
    /// deduplicates to a single shared interceptor method (verified against the generated source in
    /// <see cref="GeneratedSource_ContainsDedupedInterceptorForInterceptedEntity"/>), while still
    /// producing correct, independent results here.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_WithEligibleTypeSecondCallSite_ProducesCorrectDataTable()
    {
        var entities = new[] { new InterceptedEntity { Id = 2, Name = "Gadget" } };

        using var table = entities.ToDataTableArk();

        table.Rows[0]["Id"].Should().Be(2);
        table.Rows[0]["Name"].Should().Be("Gadget");
    }

    /// <summary>The interceptor also handles a public struct T (non-primitive value type).</summary>
    [TestMethod]
    public void ToDataTableArk_WithStructType_ProducesCorrectDataTable()
    {
        var points = new[] { new InterceptedPoint { X = 1, Y = 2 }, new InterceptedPoint { X = 3, Y = 4 } };

        using var table = points.ToDataTableArk();

        table.Columns.Count.Should().Be(2);
        table.Rows[1]["X"].Should().Be(3);
        table.Rows[1]["Y"].Should().Be(4);
    }

    /// <summary>
    /// A type with a custom base class is deliberately ineligible for interception; the call still
    /// produces correct results via the safe reflection fallback.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_WithNonFlatDerivedType_FallsBackSafelyAndProducesCorrectResult()
    {
        var entities = new[] { new InterceptedEntityDerived { BaseId = 1, DerivedName = "x" } };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(2);
        table.Rows[0]["BaseId"].Should().Be(1);
        table.Rows[0]["DerivedName"].Should().Be("x");
    }

    /// <summary>
    /// A call site inside a generic method (T is an open type parameter there) can never be
    /// intercepted; it always executes the reflection fallback regardless of the concrete type
    /// instantiated by the caller, and still produces correct results.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_ThroughGenericMethod_FallsBackSafelyAndProducesCorrectResult()
    {
        var entities = new[] { new InterceptedEntity { Id = 3, Name = "Generic" } };

        using var table = GenericFallbackHelper.ConvertGeneric(entities);

        table.Rows[0]["Id"].Should().Be(3);
        table.Rows[0]["Name"].Should().Be("Generic");
    }

    /// <summary>
    /// Reads the generator's own emitted source (via EmitCompilerGeneratedFiles) to prove that
    /// interception genuinely occurred for the InterceptedEntity call sites above: it must declare
    /// the InterceptsLocationAttribute, a method referencing InterceptedEntity's columns, and exactly
    /// two [InterceptsLocationAttribute(...)] attributes on that one shared method (one per call site
    /// in this file using that type) - proving both call-site interception and per-T deduplication.
    /// </summary>
    [TestMethod]
    public async Task GeneratedSource_ContainsDedupedInterceptorForInterceptedEntity()
    {
        var generated = await ReadGeneratedInterceptorSourceAsync().ConfigureAwait(false);

        generated.Should().Contain("InterceptsLocationAttribute");
        generated.Should().Contain("InterceptedEntity");
        generated.Should().Contain("\"Status\", typeof(global::System.String)");
        generated.Should().Contain("\"EffectiveDate\", typeof(global::System.DateTime)");

        // Both InterceptedEntity call sites above must share one method with two location attributes.
        var entityMethodIndex = generated.IndexOf("global::Ark.Tools.Core.Interceptors.Tests.InterceptedEntity> source", StringComparison.Ordinal);
        entityMethodIndex.Should().BeGreaterThan(0);
        var precedingBlock = generated[..entityMethodIndex];
        var lastAttributeBlockStart = precedingBlock.LastIndexOf("        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute", StringComparison.Ordinal);
        var attributeCount = CountOccurrences(precedingBlock[lastAttributeBlockStart..], "InterceptsLocationAttribute");
        attributeCount.Should().Be(2);
    }

    /// <summary>
    /// The generated source must not contain any interceptor for the derived (non-flat) type or for
    /// the generic-method call site, since both are documented as ineligible/un-interceptable.
    /// </summary>
    [TestMethod]
    public async Task GeneratedSource_DoesNotContainInterceptorsForIneligibleCallSites()
    {
        var generated = await ReadGeneratedInterceptorSourceAsync().ConfigureAwait(false);

        generated.Should().NotContain("InterceptedEntityDerived");
        generated.Should().NotContain("GenericFallbackHelper");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static async Task<string> ReadGeneratedInterceptorSourceAsync()
    {
        var directory = Path.GetDirectoryName(GetThisFilePath())!;
        var generatedRoot = Path.Combine(directory, "Generated");
        var file = Directory.EnumerateFiles(generatedRoot, "ToDataTableArkInterceptors.g.cs", SearchOption.AllDirectories)
            .FirstOrDefault();
        file.Should().NotBeNull("the generator should have emitted ToDataTableArkInterceptors.g.cs under {0}", generatedRoot);
        return await File.ReadAllTextAsync(file!).ConfigureAwait(false);
    }

    private static string GetThisFilePath([CallerFilePath] string path = "") => path;
}
