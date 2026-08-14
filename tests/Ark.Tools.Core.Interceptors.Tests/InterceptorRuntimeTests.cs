// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

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
        table.Rows[0].RowState.Should().Be(System.Data.DataRowState.Unchanged);
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

    /// <summary>Generated interceptors convert both supported EvolvableEnum shapes to strings.</summary>
    [TestMethod]
    public void ToDataTableArk_WithEvolvableEnumProperties_ConvertsValues()
    {
        var entities = new[]
        {
            new InterceptedEvolvableEntity
            {
                Status = Ark.Tools.Core.EvolvableEnum<InterceptedEvolvableStatus>.FromName("Future"),
                CompactStatus = Ark.Tools.Core.EvolvableEnum<InterceptedByteEvolvableStatus, byte>.FromNumber(7),
            },
            new InterceptedEvolvableEntity(),
        };

        using var table = entities.ToDataTableArk();

        table.Columns["Status"]!.DataType.Should().Be<string>();
        table.Columns["CompactStatus"]!.DataType.Should().Be<string>();
        table.Rows[0]["Status"].Should().Be("Future");
        table.Rows[0]["CompactStatus"].Should().Be("7");
        table.Rows[1].IsNull("CompactStatus").Should().BeTrue();
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

    /// <summary>Interceptor columns preserve the reflection fallback's fields-before-properties order.</summary>
    [TestMethod]
    public void ToDataTableArk_WithMixedMembers_OrdersFieldsBeforeProperties()
    {
        var entities = new[] { new MixedMemberEntity { Property = 1, Field = 2 } };

        using var table = entities.ToDataTableArk();

        table.Columns[0].ColumnName.Should().Be("Field");
        table.Columns[1].ColumnName.Should().Be("Property");
    }

    /// <summary>Types with static members use the fallback, which excludes those members.</summary>
    [TestMethod]
    public void ToDataTableArk_WithStaticMember_UsesFallback()
    {
        var entities = new[] { new StaticMemberEntity { Value = 1 } };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(1);
        table.Columns[0].ColumnName.Should().Be("Value");
        table.Rows[0]["Value"].Should().Be(1);
    }

    /// <summary>Anonymous types remain valid call sites by using the reflection fallback.</summary>
    [TestMethod]
    public void ToDataTableArk_WithAnonymousType_UsesFallback()
    {
        var entities = new[] { new { Id = 1 } };

        using var table = entities.ToDataTableArk();

        table.Rows[0]["Id"].Should().Be(1);
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
        var generated = await _readGeneratedInterceptorSourceAsync().ConfigureAwait(false);

        generated.Should().Contain("InterceptsLocationAttribute");
        generated.Should().Contain("InterceptedEntity");
        generated.Should().Contain("\"Status\", typeof(global::System.String)");
        generated.Should().Contain("\"CompactStatus\", typeof(global::System.String)");
        generated.Should().Contain("\"EffectiveDate\", typeof(global::System.DateTime)");
        generated.Should().Contain("finally");
        generated.Should().Contain("table.EndLoadData();");

        // Both InterceptedEntity call sites above must share one method with two location attributes.
        var entityMethodIndex = generated.IndexOf("global::Ark.Tools.Core.Interceptors.Tests.InterceptedEntity> source", StringComparison.Ordinal);
        entityMethodIndex.Should().BeGreaterThan(0);
        var precedingBlock = generated[..entityMethodIndex];
        var signatureStart = precedingBlock.LastIndexOf('\n') + 1;
        var attributeCount = precedingBlock[..signatureStart].TrimEnd().Split('\n')
            .Reverse()
            .TakeWhile(static line => line.Contains("InterceptsLocationAttribute", StringComparison.Ordinal))
            .Count();
        attributeCount.Should().Be(2);
    }

    /// <summary>
    /// The generated source must not contain any interceptor for the derived (non-flat) type or for
    /// the generic-method call site, since both are documented as ineligible/un-interceptable.
    /// </summary>
    [TestMethod]
    public async Task GeneratedSource_DoesNotContainInterceptorsForIneligibleCallSites()
    {
        var generated = await _readGeneratedInterceptorSourceAsync().ConfigureAwait(false);

        generated.Should().NotContain("InterceptedEntityDerived");
        generated.Should().NotContain("GenericFallbackHelper");
    }

    private static async Task<string> _readGeneratedInterceptorSourceAsync()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var generatedRoot = Path.GetFullPath(Path.Join(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "obj",
            outputDirectory.Parent!.Name,
            outputDirectory.Name,
            "Generated"));
        var file = Directory.EnumerateFiles(generatedRoot, "ToDataTableArkInterceptors.g.cs", SearchOption.AllDirectories)
            .FirstOrDefault();
        file.Should().NotBeNull("the generator should have emitted ToDataTableArkInterceptors.g.cs under {0}", generatedRoot);
        return await File.ReadAllTextAsync(file!).ConfigureAwait(false);
    }

}
