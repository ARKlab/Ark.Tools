// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;
using NodaTime;
using System.ComponentModel;

namespace Ark.Tools.Nodatime.Tests;

/// <summary>
/// <see cref="NodaTimeConverter.Register"/> must make the converters visible to the trim-safe
/// <see cref="TypeDescriptor.GetConverterFromRegisteredType(Type)"/>, which HTTP binding, Dapper handlers and JSON
/// dictionary keys use.
/// </summary>
[TestClass]
public class NodaTimeConverterRegistrationTests
{
    /// <summary>
    /// Looks the NodaTime types up before registering them, as other code may at startup:
    /// <see cref="TypeDescriptor.RegisterType{T}"/> is ignored for a type already looked up. Registering here, before
    /// any test runs, keeps the parallel converter tests from racing the registration.
    /// </summary>
    /// <param name="context">The test context.</param>
    [AssemblyInitialize]
    public static void LookUpBeforeRegistration(TestContext context)
    {
        _ = TypeDescriptor.GetConverter(typeof(LocalDate));
        _ = TypeDescriptor.GetConverter(typeof(LocalDate?));
        NodaTimeConverter.Register();
    }

    [TestMethod]
    public void RegisteredTypesResolveTheNodaTimeConverters()
    {
        NodaTimeConverter.Register();

        var instant = TypeDescriptor.GetConverterFromRegisteredType(typeof(Instant))
            .ConvertFrom(null, CultureInfo.InvariantCulture, "2026-10-09T10:00:00Z");
        instant.Should().Be(Instant.FromUtc(2026, 10, 9, 10, 0));

        var date = TypeDescriptor.GetConverterFromRegisteredType(typeof(LocalDate))
            .ConvertFrom(null, CultureInfo.InvariantCulture, "2026-10-09");
        date.Should().Be(new LocalDate(2026, 10, 9));

        TypeDescriptor.GetConverterFromRegisteredType(typeof(OffsetDateTime)).CanConvertFrom(typeof(string)).Should().BeTrue();
        TypeDescriptor.GetConverterFromRegisteredType(typeof(Instant?)).ConvertFrom(null, CultureInfo.InvariantCulture, string.Empty).Should().BeNull();
    }

    [TestMethod]
    public void RegisteredTypesReuseOneConverterInstance()
    {
        // Dapper handlers look the converter up for every value: as TypeDescriptor does, the lookup must not allocate.
        TypeDescriptor.GetConverterFromRegisteredType(typeof(LocalDate))
            .Should().BeSameAs(TypeDescriptor.GetConverterFromRegisteredType(typeof(LocalDate)));
    }
}
