// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.ComponentModel;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// <see cref="ArkTypeConverter.TryConvertSafe{T}(string?, out T)"/> resolves converters trim-safely, so a converter
/// added with <c>TypeDescriptor.AddAttributes</c> is found only when its type was registered first.
/// </summary>
[TestClass]
public class ArkTypeConverterRegisteredTypeTests
{
    [TestMethod]
    public void ConvertsTypeRegisteredBeforeItsConverterIsAdded()
    {
        TypeDescriptor.RegisterType<Registered>();
        TypeDescriptor.AddAttributes(typeof(Registered), new TypeConverterAttribute(typeof(RegisteredConverter)));

        ArkTypeConverter.TryConvertSafe<Registered>("42", out var value).Should().BeTrue();
        value.Ticks.Should().Be(42);
        ArkTypeConverter.TryConvertSafe<Registered?>("7", out var nullable).Should().BeTrue();
        nullable.Should().Be(new Registered(7));
        ArkTypeConverter.TryConvertSafe<Registered>("not-a-number", out _).Should().BeFalse();
    }

    [TestMethod]
    public void ConvertsEnumWithoutRegistration()
    {
        ArkTypeConverter.TryConvertSafe<Shade>("Dark", out var shade).Should().BeTrue();
        shade.Should().Be(Shade.Dark);
    }

    [TestMethod]
    public void ReportsMissingRegistration()
    {
        TypeDescriptor.AddAttributes(typeof(Unregistered), new TypeConverterAttribute(typeof(UnregisteredConverter)));

        var convert = static () => ArkTypeConverter.TryConvertSafe<Unregistered>("42", out _);

        convert.Should().Throw<InvalidOperationException>()
            .WithMessage("*No type converter is registered for*Unregistered*TypeDescriptor.RegisterType*");
    }

    private enum Shade
    {
        Light,
        Dark,
    }

    private readonly record struct Registered(long Ticks);

    private readonly record struct Unregistered(long Ticks);

    private sealed class RegisteredConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            return value is string text
                ? new Registered(long.Parse(text, CultureInfo.InvariantCulture))
                : base.ConvertFrom(context, culture, value);
        }
    }

    private sealed class UnregisteredConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }
    }
}
