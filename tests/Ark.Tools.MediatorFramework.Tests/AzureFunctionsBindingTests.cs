// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the conversion helpers generated Functions use for route and query values.</summary>
[TestClass]
public sealed class AzureFunctionsBindingTests
{
    [TestMethod]
    public void ParsesExplicitParsableImplementation()
    {
        ArkAzureFunctionsBinding.TryParse<Shelf>("A1", out var shelf).Should().BeTrue();
        shelf.Code.Should().Be("A1");
        ArkAzureFunctionsBinding.TryParse<Shelf>(string.Empty, out _).Should().BeFalse();
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
