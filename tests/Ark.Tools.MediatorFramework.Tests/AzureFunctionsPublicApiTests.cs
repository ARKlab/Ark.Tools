// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>
/// Guards the public API surface of the Azure Functions runtime and generator packages
/// against unreviewed drift by comparing a deterministic reflection rendering with a
/// committed baseline. Update the baseline explicitly when the public API changes.
/// </summary>
[TestClass]
public sealed class AzureFunctionsPublicApiTests
{
    /// <summary>Ark.Tools.MediatorFramework.AzureFunctions public API matches its committed baseline.</summary>
    [TestMethod]
    public async Task AzureFunctionsRuntimePublicApiMatchesBaseline()
    {
        await _assertBaselineAsync(typeof(AzureFunctions.ArkAzureFunctionsHttp).Assembly, "Ark.Tools.MediatorFramework.AzureFunctions.approved.txt");
    }

    /// <summary>Ark.Tools.MediatorFramework.AzureFunctions.Generators public API matches its committed baseline.</summary>
    [TestMethod]
    public async Task AzureFunctionsGeneratorPublicApiMatchesBaseline()
    {
        await _assertBaselineAsync(typeof(AzureFunctions.Generators.AzureFunctionsEndpointGenerator).Assembly, "Ark.Tools.MediatorFramework.AzureFunctions.Generators.approved.txt");
    }

    private static async Task _assertBaselineAsync(Assembly assembly, string baselineFileName)
    {
        var baselinePath = Path.Join(_publicApiDirectory(), baselineFileName);
        var current = _renderPublicApi(assembly);
        var baseline = File.Exists(baselinePath) ? (await File.ReadAllTextAsync(baselinePath).ConfigureAwait(false)).ReplaceLineEndings("\n") : null;
        if (!string.Equals(baseline, current, StringComparison.Ordinal))
        {
            var receivedPath = Path.ChangeExtension(baselinePath, ".received.txt");
            await File.WriteAllTextAsync(receivedPath, current).ConfigureAwait(false);
            baseline.Should().NotBeNull($"baseline '{baselineFileName}' must exist; received rendering written to '{receivedPath}'");
            baseline.Should().Be(current, $"public API drifted; review and copy '{receivedPath}' over the baseline if intended");
        }
    }

    private static string _publicApiDirectory([CallerFilePath] string sourcePath = "")
    {
        return Path.Join(Path.GetDirectoryName(sourcePath)!, "PublicApi");
    }

    private static string _renderPublicApi(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.ExportedTypes.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(_typeLine(type));
            var memberLines = new List<string>();
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!_isVisible(member))
                    continue;
                memberLines.Add($"  {_visibility(member)} {member.MemberType}: {member}");
            }
            memberLines.Sort(StringComparer.Ordinal);
            lines.AddRange(memberLines);
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string _typeLine(Type type)
    {
        var kind = type.IsEnum ? "enum" : type.IsValueType ? "struct" : type.IsInterface ? "interface" : "class";
        var bases = new List<string>();
        if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) && baseType != typeof(Enum))
            bases.Add(baseType.ToString());
        bases.AddRange(type.GetInterfaces().Where(i => i.IsPublic || i.IsNestedPublic).Select(i => i.ToString()).OrderBy(n => n, StringComparer.Ordinal));
        var suffix = bases.Count > 0 ? " : " + string.Join(", ", bases) : string.Empty;
        return $"{kind} {type.FullName}{suffix}";
    }

    private static bool _isVisible(MemberInfo member)
    {
        return member switch
        {
            ConstructorInfo ctor => ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly,
            MethodInfo method => (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly) && !method.IsSpecialName,
            FieldInfo field => (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) && !field.IsSpecialName,
            PropertyInfo property => property.GetAccessors(true).Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
            EventInfo @event => @event.AddMethod is { } add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly),
            TypeInfo => false,
            _ => false,
        };
    }

    private static string _visibility(MemberInfo member)
    {
        return member switch
        {
            MethodBase method => method.IsPublic ? "public" : "protected",
            FieldInfo field => field.IsPublic ? "public" : "protected",
            PropertyInfo property => property.GetAccessors(true).Any(a => a.IsPublic) ? "public" : "protected",
            EventInfo @event => @event.AddMethod is { IsPublic: true } ? "public" : "protected",
            _ => "public",
        };
    }
}
