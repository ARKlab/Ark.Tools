// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the conversions generated Functions use for route and query values.</summary>
[TestClass]
public sealed class AzureFunctionsBindingTests
{
    [TestMethod]
    public void ConvertsThroughConverterAddedWithAddAttributes()
    {
        // Ark.Tools.Nodatime registers its converters this way, which TypeDescriptor.GetConverterFromRegisteredType
        // does not see.
        TypeDescriptor.AddAttributes(typeof(Stamp), new TypeConverterAttribute(typeof(StampConverter)));

        ArkAzureFunctionsBinding.TryConvert<Stamp>("42", out var stamp).Should().BeTrue();
        stamp.Ticks.Should().Be(42);
        ArkAzureFunctionsBinding.TryConvert<Stamp>("not-a-number", out _).Should().BeFalse();
        ArkAzureFunctionsBinding.TryConvert<Stamp>(null, out _).Should().BeFalse();
    }

    [TestMethod]
    public void ParsesExplicitParsableImplementation()
    {
        ArkAzureFunctionsBinding.TryParse<Shelf>("A1", out var shelf).Should().BeTrue();
        shelf.Code.Should().Be("A1");
        ArkAzureFunctionsBinding.TryParse<Shelf>(string.Empty, out _).Should().BeFalse();
    }

    private readonly record struct Stamp(long Ticks);

    private sealed class StampConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            return value is string text
                ? new Stamp(long.Parse(text, CultureInfo.InvariantCulture))
                : base.ConvertFrom(context, culture, value);
        }
    }

    private readonly record struct Shelf(string Code) : IParsable<Shelf>
    {
        static Shelf IParsable<Shelf>.Parse(string s, IFormatProvider? provider)
        {
            return new Shelf(s);
        }

        static bool IParsable<Shelf>.TryParse(string? s, IFormatProvider? provider, out Shelf result)
        {
            result = new Shelf(s ?? string.Empty);
            return !string.IsNullOrEmpty(s);
        }
    }
}
