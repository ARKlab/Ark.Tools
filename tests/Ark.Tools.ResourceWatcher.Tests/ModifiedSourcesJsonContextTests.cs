// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using NodaTime;

using System.Text.Json;

namespace Ark.Tools.ResourceWatcher.Tests;

/// <summary>
/// Pins the ModifiedSources wire format to the one written by the former ConfigureArkDefaults-based serializer,
/// so rows saved before the switch to <c>ModifiedSourcesJsonContext</c> stay readable and new rows look the same.
/// </summary>
[TestClass]
public class ModifiedSourcesJsonContextTests
{
    private static readonly Dictionary<string, LocalDateTime> _sources = new(StringComparer.Ordinal)
    {
        ["SourceA"] = new LocalDateTime(2024, 1, 2, 3, 4, 5, 678),
        ["sourceB"] = new LocalDateTime(2025, 12, 31, 23, 59),
    };

    private static JsonSerializerOptions _legacyOptions()
    {
        // Same options SqlStateProvider used for ModifiedSources before the source-generated context
        var options = new JsonSerializerOptions
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        return options.ConfigureArkDefaults();
    }

    [TestMethod]
    public void Serialize_MatchesLegacyOutput()
    {
        var json = JsonSerializer.Serialize(_sources, ModifiedSourcesJsonContext.Default.ModifiedSources);

        json.Should().Be(JsonSerializer.Serialize(_sources, _legacyOptions()));
        json.Should().Be("""{"sourceA":"2024-01-02T03:04:05.678","sourceB":"2025-12-31T23:59:00"}""");
    }

    [TestMethod]
    public void Deserialize_ReadsLegacyOutput()
    {
        var legacyJson = JsonSerializer.Serialize(_sources, _legacyOptions());

        var result = JsonSerializer.Deserialize(legacyJson, ModifiedSourcesJsonContext.Default.ModifiedSources);

        result.Should().BeEquivalentTo(new Dictionary<string, LocalDateTime>(StringComparer.Ordinal)
        {
            ["sourceA"] = _sources["SourceA"],
            ["sourceB"] = _sources["sourceB"],
        });
    }
}
