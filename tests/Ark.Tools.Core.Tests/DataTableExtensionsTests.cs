// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using NodaTime;

using System.Data;
using System.Reflection;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// Tests for <see cref="DataTableExtensions.ToDataTableArk{T}(IEnumerable{T})"/> covering the
/// mixed-type shredding behavior of the generic reflection fallback in
/// <c>ShredObjectToDataTable&lt;T&gt;</c>: column schema derivation, value conversions (enums,
/// NodaTime types, nullable value types), the primitive-scalar path, the existing-table/ordinal-map
/// path (via <see cref="DataTableExtensions.ToDataTable{T}(IEnumerable{T}, DataTable, LoadOption?)"/>),
/// and explicit invocation via reflection to guarantee the reflection fallback is exercised
/// independently of any compile-time interceptor that may be active in the compilation.
/// </summary>
[TestClass]
public class DataTableExtensionsTests
{
    private enum Status
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
    }

    // Exactly 10 mixed-type public properties, matching the shape used by the performance benchmarks.
    private sealed class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Measurement { get; set; }
        public decimal Amount { get; set; }
        public bool IsEnabled { get; set; }
        public int? OptionalCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Status State { get; set; }
        public Guid CorrelationId { get; set; }
        public LocalDate EffectiveDate { get; set; }
    }

    private sealed class EntityWithField
    {
        public int FieldValue;
        public string PropValue { get; set; } = string.Empty;
    }

    // Type.GetFields()/GetProperties() include public static/const members by default (not just
    // instance members); the reflection fallback has always shredded these too (with a constant
    // value repeated on every row), so this fixture guards that historical, if unusual, behavior.
    private sealed class EntityWithStaticMember
    {
        public const int Constant = 7;
        public static string StaticProperty => "static-value";
        public int InstanceValue { get; set; }
    }

    private sealed class NodaTimeEntity
    {
        public LocalDate LocalDate { get; set; }
        public LocalDateTime LocalDateTime { get; set; }
        public Instant Instant { get; set; }
        public OffsetDateTime OffsetDateTime { get; set; }
        public OffsetDate OffsetDate { get; set; }
        public LocalTime LocalTime { get; set; }
        public LocalDate? NullableLocalDate { get; set; }
        public LocalTime? NullableLocalTime { get; set; }
    }

    private sealed class NullableEntity
    {
        public int? NullableInt { get; set; }
        public bool? NullableBool { get; set; }
        public Status? NullableEnum { get; set; }
    }

    private sealed class Empty
    {
    }

    private sealed class RuntimeTypedEntity
    {
        public object? Value { get; set; }
    }

    /// <summary>All 10 mixed-type columns are created with the expected .NET column types.</summary>
    [TestMethod]
    public void ToDataTableArk_WithMixedTypeProperties_CreatesExpectedColumnSchema()
    {
        var entities = new[] { new Entity() };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(10);
        table.Columns["Id"]!.DataType.Should().Be<int>();
        table.Columns["Name"]!.DataType.Should().Be<string>();
        table.Columns["Measurement"]!.DataType.Should().Be<double>();
        table.Columns["Amount"]!.DataType.Should().Be<decimal>();
        table.Columns["IsEnabled"]!.DataType.Should().Be<bool>();
        table.Columns["OptionalCount"]!.DataType.Should().Be<int>();
        table.Columns["CreatedAt"]!.DataType.Should().Be<DateTime>();
        table.Columns["State"]!.DataType.Should().Be<string>();
        table.Columns["CorrelationId"]!.DataType.Should().Be<Guid>();
        table.Columns["EffectiveDate"]!.DataType.Should().Be<DateTime>();
    }

    /// <summary>Column values for a fully-populated row match the source object exactly.</summary>
    [TestMethod]
    public void ToDataTableArk_WithPopulatedRow_MapsAllValuesCorrectly()
    {
        var entity = new Entity
        {
            Id = 42,
            Name = "Widget",
            Measurement = 3.14,
            Amount = 9.99m,
            IsEnabled = true,
            OptionalCount = 7,
            CreatedAt = new DateTime(2024, 5, 1, 10, 30, 0, DateTimeKind.Utc),
            State = Status.Active,
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            EffectiveDate = new LocalDate(2024, 5, 1),
        };

        using var table = new[] { entity }.ToDataTableArk();

        var row = table.Rows[0];
        row["Id"].Should().Be(42);
        row["Name"].Should().Be("Widget");
        row["Measurement"].Should().Be(3.14);
        row["Amount"].Should().Be(9.99m);
        row["IsEnabled"].Should().Be(true);
        row["OptionalCount"].Should().Be(7);
        row["CreatedAt"].Should().Be(entity.CreatedAt);
        row["State"].Should().Be("Active");
        row["CorrelationId"].Should().Be(entity.CorrelationId);
        row["EffectiveDate"].Should().Be(entity.EffectiveDate.ToDateTimeUnspecified());
    }

    /// <summary>A null nullable-value property is stored as DBNull, not converted.</summary>
    [TestMethod]
    public void ToDataTableArk_WithNullNullableProperty_StoresDbNull()
    {
        var entity = new Entity { OptionalCount = null };

        using var table = new[] { entity }.ToDataTableArk();

        table.Rows[0].IsNull("OptionalCount").Should().BeTrue();
    }

    /// <summary>Enum properties are converted to their string member name, and the column type is string.</summary>
    [TestMethod]
    public void ToDataTableArk_WithEnumProperty_ConvertsToMemberNameString()
    {
        var entities = new[] { new Entity { State = Status.Completed } };

        using var table = entities.ToDataTableArk();

        table.Columns["State"]!.DataType.Should().Be<string>();
        table.Rows[0]["State"].Should().Be("Completed");
    }

    /// <summary>A nullable enum with a value converts to its string name; null converts to DBNull.</summary>
    [TestMethod]
    public void ToDataTableArk_WithNullableEnum_ConvertsValueOrNull()
    {
        var entities = new[]
        {
            new NullableEntity { NullableEnum = Status.Active },
            new NullableEntity { NullableEnum = null },
        };

        using var table = entities.ToDataTableArk();

        table.Columns["NullableEnum"]!.DataType.Should().Be<string>();
        table.Rows[0]["NullableEnum"].Should().Be("Active");
        table.Rows[1].IsNull("NullableEnum").Should().BeTrue();
    }

    /// <summary>Runtime enum and NodaTime values retain historical conversions when declared as object.</summary>
    [TestMethod]
    public void ToDataTableArk_WithRuntimeTypedValues_ConvertsValues()
    {
        var entities = new[]
        {
            new RuntimeTypedEntity { Value = Status.Active },
            new RuntimeTypedEntity { Value = new LocalDate(2024, 5, 1) },
        };

        using var table = entities.ToDataTableArk();

        table.Rows[0]["Value"].Should().Be("Active");
        table.Rows[1]["Value"].Should().Be(new DateTime(2024, 5, 1));
    }

    /// <summary>Plain nullable value types (not enums, not NodaTime) round-trip both null and non-null values.</summary>
    [TestMethod]
    public void ToDataTableArk_WithPlainNullableValueTypes_RoundTripsNullAndValue()
    {
        var entities = new[]
        {
            new NullableEntity { NullableInt = 5, NullableBool = true },
            new NullableEntity { NullableInt = null, NullableBool = null },
        };

        using var table = entities.ToDataTableArk();

        table.Columns["NullableInt"]!.DataType.Should().Be<int>();
        table.Columns["NullableBool"]!.DataType.Should().Be<bool>();
        table.Rows[0]["NullableInt"].Should().Be(5);
        table.Rows[0]["NullableBool"].Should().Be(true);
        table.Rows[1].IsNull("NullableInt").Should().BeTrue();
        table.Rows[1].IsNull("NullableBool").Should().BeTrue();
    }

    /// <summary>All six NodaTime conversions used by ShredObjectToDataTable produce the documented .NET equivalents.</summary>
    [TestMethod]
    public void ToDataTableArk_WithNodaTimeProperties_ConvertsToDocumentedDotNetTypes()
    {
        var localDate = new LocalDate(2024, 3, 15);
        var localDateTime = new LocalDateTime(2024, 3, 15, 8, 30, 0);
        var instant = Instant.FromUtc(2024, 3, 15, 8, 30, 0);
        var offsetDateTime = new OffsetDateTime(localDateTime, Offset.FromHours(2));
        var offsetDate = new OffsetDate(localDate, Offset.FromHours(2));
        var localTime = new LocalTime(14, 45, 30);

        var entity = new NodaTimeEntity
        {
            LocalDate = localDate,
            LocalDateTime = localDateTime,
            Instant = instant,
            OffsetDateTime = offsetDateTime,
            OffsetDate = offsetDate,
            LocalTime = localTime,
            NullableLocalDate = localDate,
            NullableLocalTime = localTime,
        };

        using var table = new[] { entity }.ToDataTableArk();

        table.Columns["LocalDate"]!.DataType.Should().Be<DateTime>();
        table.Columns["LocalDateTime"]!.DataType.Should().Be<DateTime>();
        table.Columns["Instant"]!.DataType.Should().Be<DateTime>();
        table.Columns["OffsetDateTime"]!.DataType.Should().Be<DateTimeOffset>();
        table.Columns["OffsetDate"]!.DataType.Should().Be<DateTimeOffset>();
        table.Columns["LocalTime"]!.DataType.Should().Be<TimeSpan>();
        table.Columns["NullableLocalDate"]!.DataType.Should().Be<DateTime>();
        table.Columns["NullableLocalTime"]!.DataType.Should().Be<TimeSpan>();

        var row = table.Rows[0];
        row["LocalDate"].Should().Be(localDate.ToDateTimeUnspecified());
        row["LocalDateTime"].Should().Be(localDateTime.ToDateTimeUnspecified());
        row["Instant"].Should().Be(instant.ToDateTimeUtc());
        row["OffsetDateTime"].Should().Be(offsetDateTime.ToDateTimeOffset());
        row["OffsetDate"].Should().Be(offsetDate.At(LocalTime.Midnight).ToDateTimeOffset());
        row["LocalTime"].Should().Be(TimeSpan.FromTicks(localTime.TickOfDay));
        row["NullableLocalDate"].Should().Be(localDate.ToDateTimeUnspecified());
        row["NullableLocalTime"].Should().Be(TimeSpan.FromTicks(localTime.TickOfDay));
    }

    /// <summary>Nullable NodaTime properties with no value convert to DBNull rather than a default DateTime/TimeSpan.</summary>
    [TestMethod]
    public void ToDataTableArk_WithNullNodaTimeNullableProperties_StoresDbNull()
    {
        var entity = new NodaTimeEntity
        {
            NullableLocalDate = null,
            NullableLocalTime = null,
        };

        using var table = new[] { entity }.ToDataTableArk();

        table.Rows[0].IsNull("NullableLocalDate").Should().BeTrue();
        table.Rows[0].IsNull("NullableLocalTime").Should().BeTrue();
    }

    /// <summary>Public fields are shredded together with properties, fields ordered before properties.</summary>
    [TestMethod]
    public void ToDataTableArk_WithPublicField_ShredsFieldsAndPropertiesTogether()
    {
        var entities = new[] { new EntityWithField { FieldValue = 3, PropValue = "x" } };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(2);
        table.Columns[0].ColumnName.Should().Be("FieldValue");
        table.Columns[1].ColumnName.Should().Be("PropValue");
        table.Rows[0]["FieldValue"].Should().Be(3);
        table.Rows[0]["PropValue"].Should().Be("x");
    }

    /// <summary>
    /// Public static/const members (which Type.GetFields()/GetProperties() include by default) are
    /// shredded as extra columns holding their constant value on every row, matching the historical
    /// reflection behavior (FieldInfo/PropertyInfo.GetValue ignores the instance for static members).
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_WithStaticMembers_ShredsConstantValueOnEveryRow()
    {
        var entities = new[]
        {
            new EntityWithStaticMember { InstanceValue = 1 },
            new EntityWithStaticMember { InstanceValue = 2 },
        };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(3);
        table.Rows[0]["Constant"].Should().Be(7);
        table.Rows[1]["Constant"].Should().Be(7);
        table.Rows[0]["StaticProperty"].Should().Be("static-value");
        table.Rows[0]["InstanceValue"].Should().Be(1);
        table.Rows[1]["InstanceValue"].Should().Be(2);
    }

    /// <summary>A primitive element type (int) is shredded into a single scalar "Value" column.</summary>
    [TestMethod]
    public void ToDataTableArk_WithPrimitiveElementType_CreatesSingleValueColumn()
    {
        int[] values = [1, 2, 3];

        using var table = values.ToDataTableArk();

        table.Columns.Count.Should().Be(1);
        table.Columns[0].ColumnName.Should().Be("Value");
        table.Columns[0].DataType.Should().Be<int>();
        table.Rows.Count.Should().Be(3);
        table.Rows[1]["Value"].Should().Be(2);
    }

    /// <summary>An empty source sequence produces a table with the correct schema and zero rows.</summary>
    [TestMethod]
    public void ToDataTableArk_WithEmptySource_ProducesEmptySchemaOnlyTable()
    {
        using var table = Array.Empty<Entity>().ToDataTableArk();

        table.Columns.Count.Should().Be(10);
        table.Rows.Count.Should().Be(0);
    }

    /// <summary>A type with no public fields or properties produces a zero-column table with one row per element.</summary>
    [TestMethod]
    public void ToDataTableArk_WithNoPublicMembers_ProducesZeroColumnTableWithOneRowPerElement()
    {
        var entities = new Empty?[] { new(), new() };

        using var table = entities.ToDataTableArk();

        table.Columns.Count.Should().Be(0);
        table.Rows.Count.Should().Be(2);
    }

    /// <summary>
    /// A null element for a reference-type T throws TargetException with the same message reflection
    /// would produce, preserving the historical behavior even after switching to compiled accessors.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_WithNullElementForReferenceType_ThrowsTargetException()
    {
        var entities = new Entity?[] { new(), null };

        var act = () => entities.ToDataTableArk();

        act.Should().Throw<TargetException>()
            .WithMessage("Non-static method requires a target.");
    }

    /// <summary>
    /// The existing-table/ordinal-map path (used when passing a pre-existing DataTable) shares the
    /// same optimized member accessors and produces identical values to the fast new-table path.
    /// </summary>
    [TestMethod]
    public void ToDataTable_WithExistingTable_UsesOrdinalMapAndProducesSameValues()
    {
        using var existing = new DataTable("Existing");
        existing.Columns.Add("Id", typeof(int));
        existing.Columns.Add("Name", typeof(string));
        existing.Columns.Add("Measurement", typeof(double));
        existing.Columns.Add("Amount", typeof(decimal));
        existing.Columns.Add("IsEnabled", typeof(bool));
        existing.Columns.Add("OptionalCount", typeof(int));
        existing.Columns.Add("CreatedAt", typeof(DateTime));
        existing.Columns.Add("State", typeof(string));
        existing.Columns.Add("CorrelationId", typeof(Guid));
        existing.Columns.Add("EffectiveDate", typeof(DateTime));

        var entity = new Entity
        {
            Id = 1,
            Name = "A",
            State = Status.Pending,
            EffectiveDate = new LocalDate(2024, 1, 1),
        };

        var result = new[] { entity }.ToDataTable(existing, LoadOption.OverwriteChanges);

        ReferenceEquals(result, existing).Should().BeTrue();
        result.Rows[0]["Id"].Should().Be(1);
        result.Rows[0]["Name"].Should().Be("A");
        result.Rows[0]["State"].Should().Be("Pending");
        result.Rows[0]["EffectiveDate"].Should().Be(entity.EffectiveDate.ToDateTimeUnspecified());
    }

    /// <summary>An enumeration failure still ends bulk-load mode and restores row-change notifications.</summary>
    [TestMethod]
    public void ToDataTable_WhenEnumerationThrows_RestoresNotifications()
    {
        using var table = new DataTable();

        var act = () => ThrowAfterFirst().ToDataTable(table, null);

        act.Should().Throw<InvalidOperationException>().WithMessage("Enumeration failed.");

        var rowChangedCount = 0;
        table.RowChanged += (_, _) => rowChangedCount++;
        table.Rows.Add(table.NewRow());

        rowChangedCount.Should().Be(1);
    }

    /// <summary>
    /// Invoking ToDataTableArk via reflection (MethodInfo.MakeGenericMethod().Invoke) guarantees the
    /// call site is invisible to any C# 14 interceptor (which can only intercept calls that are
    /// syntactically resolvable at compile time), so this exercises the reflection-based fallback
    /// implementation directly and end-to-end regardless of whether the source generator is wired
    /// into this test project.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_InvokedViaReflection_ExercisesFallbackAndProducesCorrectResult()
    {
        var entities = new[] { new Entity { Id = 99, Name = "Reflected", State = Status.Completed } };

        var method = typeof(DataTableExtensions).GetMethod(nameof(DataTableExtensions.ToDataTableArk))!
            .MakeGenericMethod(typeof(Entity));

        using var table = (DataTable)method.Invoke(null, [entities])!;

        table.Columns.Count.Should().Be(10);
        table.Rows[0]["Id"].Should().Be(99);
        table.Rows[0]["Name"].Should().Be("Reflected");
        table.Rows[0]["State"].Should().Be("Completed");
    }

    /// <summary>
    /// Invoking ToDataTableArk via reflection with a null element for a reference-type T still throws
    /// the same TargetException as a direct call, proving the reflection fallback's null-instance
    /// guard behaves identically whether reached via a normal call or via reflection.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_InvokedViaReflectionWithNullElement_ThrowsTargetException()
    {
        var entities = new Entity?[] { null };

        var method = typeof(DataTableExtensions).GetMethod(nameof(DataTableExtensions.ToDataTableArk))!
            .MakeGenericMethod(typeof(Entity));

        var act = () => method.Invoke(null, [entities]);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<TargetException>()
            .WithMessage("Non-static method requires a target.");
    }

    /// <summary>
    /// Calling ToDataTableArk twice for the same T reuses the statically cached schema/accessor plan:
    /// this is a behavioral (not timing-based) proxy for "prepared once per T in static
    /// initialization" by asserting repeated calls remain consistent and independent across sequences.
    /// </summary>
    [TestMethod]
    public void ToDataTableArk_CalledTwiceForSameType_ProducesIndependentTablesWithConsistentSchema()
    {
        using var first = new[] { new Entity { Id = 1 } }.ToDataTableArk();
        using var second = new[] { new Entity { Id = 2 }, new Entity { Id = 3 } }.ToDataTableArk();

        first.Columns.Count.Should().Be(second.Columns.Count);
        first.Rows.Count.Should().Be(1);
        second.Rows.Count.Should().Be(2);
        second.Rows[1]["Id"].Should().Be(3);
    }

    private static IEnumerable<Entity> ThrowAfterFirst()
    {
        yield return new Entity { Id = 1 };
        throw new InvalidOperationException("Enumeration failed.");
    }
}
