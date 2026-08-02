// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

namespace Ark.Tools.Core.Benchmarks;

/// <summary>
/// Fixture status used by <see cref="BenchmarkEntity.Status"/> to exercise the
/// enum-to-string column conversion performed by ToDataTableArk.
/// </summary>
public enum BenchmarkStatus
{
    /// <summary>Represents a pending item.</summary>
    Pending = 0,

    /// <summary>Represents an active item.</summary>
    Active = 1,

    /// <summary>Represents a completed item.</summary>
    Completed = 2,
}

/// <summary>
/// Plain object exposing exactly 10 mixed-type public properties, used as the
/// fixed shape for the ToDataTableArk performance benchmarks. The property set
/// intentionally spans primitives, string, decimal, nullable, enum, and NodaTime
/// types so that both the "direct" and "converted" column code paths are measured.
/// </summary>
public sealed class BenchmarkEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a floating point measurement.</summary>
    public double Measurement { get; set; }

    /// <summary>Gets or sets a monetary amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets a flag.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets an optional counter, exercising the nullable-value column path.</summary>
    public int? OptionalCount { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the status, exercising the enum-to-string column conversion.</summary>
    public BenchmarkStatus Status { get; set; }

    /// <summary>Gets or sets a unique identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets a NodaTime local date, exercising the NodaTime-to-DateTime column conversion.</summary>
    public LocalDate EffectiveDate { get; set; }

    /// <summary>Creates a deterministic sequence of <paramref name="count"/> entities for benchmarking.</summary>
    /// <param name="count">The number of entities to create.</param>
    /// <returns>An array of entities with deterministic, varied values.</returns>
    public static BenchmarkEntity[] CreateMany(int count)
    {
        var items = new BenchmarkEntity[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = new BenchmarkEntity
            {
                Id = i,
                Name = "Entity-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Measurement = i * 1.5,
                Amount = i * 0.1m,
                IsEnabled = i % 2 == 0,
                OptionalCount = i % 3 == 0 ? null : i,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                Status = (BenchmarkStatus)(i % 3),
                CorrelationId = Guid.NewGuid(),
                EffectiveDate = new LocalDate(2024, 1, 1).PlusDays(i % 28),
            };
        }

        return items;
    }
}
