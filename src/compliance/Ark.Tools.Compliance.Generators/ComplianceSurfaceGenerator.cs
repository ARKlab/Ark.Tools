// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Compliance.Generators;

/// <summary>Inventories classified members and gates changes against a reviewed compliance baseline.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ComplianceSurfaceGenerator : IIncrementalGenerator
{
    private const string Header = "COMPLIANCE-SURFACE 1";
    private const string Prefix = "Ark.Tools.Compliance.";

    private static readonly DiagnosticDescriptor _drift = new(
        "ARKPII020", "Compliance surface changed",
        "Compliance surface differs from ArkComplianceSurface.txt: {0}. Review obj/.../ArkComplianceSurface.current.txt and copy it to ArkComplianceSurface.txt to accept this change.",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _weakened = new(
        "ARKPII021", "Classified member removed or weakened",
        "Classified member '{0}' is absent from the baseline or its classification has been weakened. Review this privacy change before updating ArkComplianceSurface.txt",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inventory = context.CompilationProvider.Select(static (compilation, token) => _build(compilation, token));
        var baselines = context.AdditionalTextsProvider
            .Where(static file => string.Equals(Path.GetFileName(file.Path), "ArkComplianceSurface.txt", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => file.GetText(token)?.ToString())
            .Collect();
        var enabled = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            !(options.GlobalOptions.TryGetValue("build_property.EnableArkToolsCompliance", out var compliance)
                && string.Equals(compliance, "false", StringComparison.OrdinalIgnoreCase))
            && options.GlobalOptions.TryGetValue("build_property.ArkComplianceSurfaceEnabled", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            && !(options.GlobalOptions.TryGetValue("build_property.ArkComplianceSurfaceUpdating", out var updating)
                && string.Equals(updating, "true", StringComparison.OrdinalIgnoreCase)));

        context.RegisterSourceOutput(inventory, static (production, surface) =>
            production.AddSource("ArkComplianceSurface.g.cs", "/*\n" + surface.Text + "*/\n"));
        context.RegisterSourceOutput(inventory.Combine(baselines).Combine(enabled), static (production, input) =>
        {
            var ((surface, files), isEnabled) = input;
            if (isEnabled)
                _verify(production, surface, files);
        });
    }

    private static Surface _build(Compilation compilation, CancellationToken token)
    {
        var types = _types(compilation.Assembly.GlobalNamespace).ToArray();
        var notes = _revealNotes(compilation, token);
        var registrations = _registrations(compilation, token);
        var transport = _transportTypes(types, token);
        var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            token.ThrowIfCancellationRequested();
            foreach (var member in _members(type))
            {
                var key = _key(member);
                members.Add(key);
                var valueType = _valueType(member);
                var classifications = new SortedSet<string>(StringComparer.Ordinal);
                _classifications(member, classifications);
                _classifications(type, classifications);
                _typeClassifications(valueType, classifications, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
                if (classifications.Count == 0)
                    continue;

                var purposeNotes = new SortedSet<string>(StringComparer.Ordinal);
                _documentation(member, purposeNotes, token);
                if (notes.TryGetValue(key, out var purposes))
                    purposeNotes.UnionWith(purposes);
                var egress = _egress(member, valueType, registrations, transport);
                var fields = new[]
                {
                    "CLASSIFIED", _name(type), _memberName(member), string.Join(",", classifications),
                    string.Join("; ", purposeNotes), string.Join(",", egress),
                };
                entries[key] = new Entry(key, string.Join("\t", fields.Select(_escape)),
                    classifications.ToImmutableArray(), member.Locations.FirstOrDefault() ?? Location.None);
            }
        }

        return new Surface(Header + "\n" + string.Join(string.Empty, entries.Values
            .OrderBy(static entry => entry.Line, StringComparer.Ordinal).Select(static entry => entry.Line + "\n")), entries, members);
    }

    private static IEnumerable<INamedTypeSymbol> _types(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in _types(type))
                    yield return nested;
            }
            else if (member is INamespaceSymbol ns)
            {
                foreach (var nested in _types(ns))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<ISymbol> _members(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol property
                && (!property.IsImplicitlyDeclared || property.DeclaringSyntaxReferences.Length > 0))
                yield return property;
            else if (member is IFieldSymbol field && !field.IsImplicitlyDeclared)
                yield return field;
            else if (member is IMethodSymbol method && !method.IsImplicitlyDeclared
                && method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor)
            {
                foreach (var parameter in method.Parameters)
                    yield return parameter;
            }
        }
    }

    private static ITypeSymbol? _valueType(ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }

    private static void _classifications(ISymbol symbol, SortedSet<string> output)
    {
        foreach (var name in symbol.GetAttributes().Select(static attribute => attribute.AttributeClass?.ToDisplayString()))
        {
            if (name == Prefix + "PersonalDataAttribute")
                output.Add("Ark:PersonalData");
            else if (name == Prefix + "SensitivePersonalDataAttribute")
                output.Add("Ark:SensitivePersonalData");
            else if (name == Prefix + "SecretAttribute")
                output.Add("Ark:Secret");
            else if (name == Prefix + "PseudonymousAttribute")
                output.Add("Ark:Pseudonymous");
        }
        if (symbol is IPropertySymbol { OverriddenProperty: { } overridden })
            _classifications(overridden, output);
        if (symbol is INamedTypeSymbol { BaseType: { } baseType })
            _classifications(baseType, output);
    }

    private static void _typeClassifications(ITypeSymbol? type, SortedSet<string> output, HashSet<ITypeSymbol> seen)
    {
        if (type is null || !seen.Add(type))
            return;
        _classifications(type, output);
        if (type is IArrayTypeSymbol array)
            _typeClassifications(array.ElementType, output, seen);
        if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
                _typeClassifications(argument, output, seen);
        }
    }

    private static void _documentation(ISymbol symbol, SortedSet<string> notes, CancellationToken token)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: token);
        if (string.IsNullOrWhiteSpace(xml))
            return;
        try
        {
            var summary = XElement.Parse(xml).Element("summary")?.Value;
            if (!string.IsNullOrWhiteSpace(summary))
                notes.Add(string.Join(" ", summary!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        }
        catch (XmlException)
        {
            // The compiler reports malformed documentation; it must not crash the inventory.
        }
    }

    private static Dictionary<string, SortedSet<string>> _revealNotes(Compilation compilation, CancellationToken token)
    {
        var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(token).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                token.ThrowIfCancellationRequested();
                if (invocation.Expression is not MemberAccessExpressionSyntax access || access.Name.Identifier.ValueText != "Reveal"
                    || invocation.ArgumentList.Arguments.Count != 1
                    || model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
                    || method.Parameters.Length != 1
                    || method.Parameters[0].Type.ToDisplayString() != Prefix + "CompliancePurpose"
                    || model.GetSymbolInfo(access.Expression, token).Symbol is not { } member)
                    continue;

                var expression = invocation.ArgumentList.Arguments[0].Expression;
                var purpose = model.GetSymbolInfo(expression, token).Symbol;
                string? text = null;
                string? category = null;
                if (purpose is IPropertySymbol property && property.ContainingType.ToDisplayString() == Prefix + "CompliancePurpose")
                {
                    text = property.Name;
                    category = _builtInCategory(property, token);
                }
                else if (expression is InvocationExpressionSyntax custom
                    && purpose is IMethodSymbol { Name: "Custom" } customMethod
                    && customMethod.ContainingType.ToDisplayString() == Prefix + "CompliancePurpose"
                    && custom.ArgumentList.Arguments.Count == 2)
                {
                    var constant = model.GetConstantValue(custom.ArgumentList.Arguments[0].Expression, token);
                    if (constant.HasValue && constant.Value is string value)
                        text = value;
                    var categorySymbol = model.GetSymbolInfo(custom.ArgumentList.Arguments[1].Expression, token).Symbol;
                    if (categorySymbol is IFieldSymbol categoryField
                        && categoryField.ContainingType.ToDisplayString() == Prefix + "CompliancePurposeCategory")
                    {
                        category = categoryField.Name;
                    }
                }
                _add(result, _key(member), "Reveal: " + (text ?? "(dynamic purpose)") + " [" + (category ?? "(dynamic category)") + "]");
            }
        }
        return result;
    }

    // Resolves the category a built-in CompliancePurpose property (for example SendTransactionalEmail)
    // carries: from its declaration syntax when compiled from source, else from the known built-in map
    // (metadata references expose no syntax and enum property bodies are not evaluable in a generator).
    // ponytail: the map must be kept in sync with CompliancePurpose's built-in properties; a miss only
    // degrades the note to "(dynamic category)".
    private static string? _builtInCategory(IPropertySymbol property, CancellationToken token)
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            foreach (var access in reference.GetSyntax(token).DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Expression is IdentifierNameSyntax { Identifier.ValueText: "CompliancePurposeCategory" }
                    || (access.Expression is MemberAccessExpressionSyntax nested && nested.Name.Identifier.ValueText == "CompliancePurposeCategory"))
                {
                    return access.Name.Identifier.ValueText;
                }
            }
        }

        return property.Name switch
        {
            "SendTransactionalEmail" => "CustomerSupport",
            _ => null,
        };
    }

    private static Dictionary<string, SortedSet<string>> _registrations(Compilation compilation, CancellationToken token)
    {
        var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(token).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                token.ThrowIfCancellationRequested();
                if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
                    || method.Name is not ("Register" or "RegisterBuiltIn"))
                    continue;
                var serializer = method.ContainingType.ToDisplayString() switch
                {
                    Prefix + "Dapper.SensitiveValueDapper" => "Dapper",
                    Prefix + "NewtonsoftJson.SensitiveValueNewtonsoftJson" => "Newtonsoft.Json",
                    Prefix + "Protobuf.SensitiveValueProtobuf" => "Protobuf",
                    Prefix + "MessagePack.SensitiveValueFormatterResolver" => "MessagePack",
                    _ => null,
                };
                if (serializer is null)
                    continue;
                if (method.Name == "RegisterBuiltIn")
                {
                    foreach (var name in new[] { "EmailAddress", "PhoneNumber", "PersonName", "PostalAddressLine", "NationalIdentifier", "ApiKey" })
                        _add(result, Prefix + name, serializer);
                }
                else
                {
                    foreach (var argument in method.TypeArguments)
                        _add(result, _name(argument), serializer);
                }
            }
        }
        return result;
    }

    private static Dictionary<string, SortedSet<string>> _transportTypes(INamedTypeSymbol[] types, CancellationToken token)
    {
        var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var transports = type.GetAttributes()
                .Select(static attribute => attribute.AttributeClass?.ToDisplayString() switch
                {
                    "Ark.Tools.MediatorFramework.HttpEndpointAttribute" => "Http",
                    "Ark.Tools.MediatorFramework.GrpcMethodAttribute" => "Grpc",
                    "Ark.Tools.MediatorFramework.RebusMessageAttribute" => "Rebus",
                    "Ark.Tools.MediatorFramework.MessageAttribute" => "Message",
                    "Ark.Tools.MediatorFramework.EventAttribute" => "Event",
                    _ => null,
                })
                .Where(static transport => transport is not null);
            foreach (var transport in transports)
            {
                var target = transport + ":" + _name(type);
                var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var path = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                _walkTransport(type, target, result, visited, path, token);
                foreach (var contract in type.AllInterfaces)
                {
                    foreach (var argument in contract.TypeArguments)
                        _walkTransport(argument, target, result, visited, path, token);
                }
            }
        }
        return result;
    }

    private static void _walkTransport(ITypeSymbol type, string target, Dictionary<string, SortedSet<string>> result,
        HashSet<ITypeSymbol> visited, HashSet<INamedTypeSymbol> path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!visited.Add(type))
            return;
        _add(result, _name(type), target);
        if (type is IArrayTypeSymbol array)
            _walkTransport(array.ElementType, target, result, visited, path, token);
        if (type is not INamedTypeSymbol named)
            return;
        _add(result, _name(named.OriginalDefinition), target);
        foreach (var argument in named.TypeArguments)
            _walkTransport(argument, target, result, visited, path, token);
        if (!path.Add(named.OriginalDefinition))
            return;
        if (named.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            _walkTransport(baseType, target, result, visited, path, token);
        if (named.Locations.Any(static location => location.IsInSource))
        {
            foreach (var (member, value) in named.GetMembers()
                .Select(member => (member, value: _valueType(member)))
                .Where(item => item.value is not null
                    && _isSerializableMember(item.member)
                    && !_ignoredByTransport(item.member, target)))
            {
                _walkTransport(value!, target, result, visited, path, token);
            }
        }
        path.Remove(named.OriginalDefinition);
    }

    private static SortedSet<string> _egress(ISymbol member, ITypeSymbol? valueType,
        Dictionary<string, SortedSet<string>> registrations, Dictionary<string, SortedSet<string>> transport)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        if (!_isSerializableMember(member))
            return result;
        if (transport.TryGetValue(_name(member.ContainingType), out var targets))
            result.UnionWith(targets.Where(target => !_ignoredByTransport(member, target)));
        _valueEgress(valueType, result, registrations, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        foreach (var serializer in member.GetAttributes().Concat(member.ContainingType.GetAttributes())
            .Select(static attribute => attribute.AttributeClass?.ToDisplayString() switch
            {
                "System.Text.Json.Serialization.JsonPropertyNameAttribute" => "System.Text.Json",
                "System.Text.Json.Serialization.JsonIncludeAttribute" => "System.Text.Json",
                "Newtonsoft.Json.JsonPropertyAttribute" => "Newtonsoft.Json",
                "ProtoBuf.ProtoMemberAttribute" => "Protobuf",
                "MessagePack.KeyAttribute" => "MessagePack",
                _ => null,
            })
            .Where(static serializer => serializer is not null))
        {
            result.Add(serializer!);
        }
        if (_hasAttribute(member, "System.Text.Json.Serialization.JsonIgnoreAttribute", alwaysOnly: true))
            result.Remove("System.Text.Json");
        if (_hasAttribute(member, "Newtonsoft.Json.JsonIgnoreAttribute"))
            result.Remove("Newtonsoft.Json");
        if (_hasAttribute(member, "ProtoBuf.ProtoIgnoreAttribute"))
            result.Remove("Protobuf");
        if (_hasAttribute(member, "MessagePack.IgnoreMemberAttribute"))
            result.Remove("MessagePack");
        return result;
    }

    private static bool _ignoredByTransport(ISymbol member, string target)
    {
        return (target.StartsWith("Http:", StringComparison.Ordinal)
                && _hasAttribute(member, "System.Text.Json.Serialization.JsonIgnoreAttribute", alwaysOnly: true))
            || (target.StartsWith("Grpc:", StringComparison.Ordinal) && _hasAttribute(member, "ProtoBuf.ProtoIgnoreAttribute"));
    }

    private static bool _isSerializableMember(ISymbol member)
    {
        return !member.IsStatic && member is not IParameterSymbol
            && (member is IPropertySymbol { IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public }
                || member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public }
                || _hasAttribute(member, "System.Text.Json.Serialization.JsonIncludeAttribute")
                || _hasAttribute(member, "Newtonsoft.Json.JsonPropertyAttribute")
                || _hasAttribute(member, "ProtoBuf.ProtoMemberAttribute")
                || _hasAttribute(member, "MessagePack.KeyAttribute"));
    }

    private static void _valueEgress(ITypeSymbol? type, SortedSet<string> result,
        Dictionary<string, SortedSet<string>> registrations, HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type))
            return;
        if (registrations.TryGetValue(_name(type), out var serializers))
            result.UnionWith(serializers);
        if (type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.OriginalDefinition.ToDisplayString() == Prefix + "SensitiveValueObjectAttribute<T>"))
            result.Add("System.Text.Json");
        if (type is IArrayTypeSymbol array)
            _valueEgress(array.ElementType, result, registrations, visited);
        if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
                _valueEgress(argument, result, registrations, visited);
        }
    }

    private static bool _hasAttribute(ISymbol symbol, string name, bool alwaysOnly = false)
    {
        return symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == name
            && (!alwaysOnly || !attribute.NamedArguments.Any(static argument => argument.Key == "Condition")
                || attribute.NamedArguments.Any(static argument => argument.Key == "Condition" && argument.Value.Value is 1)));
    }

    private static void _add(Dictionary<string, SortedSet<string>> result, string key, string value)
    {
        if (!result.TryGetValue(key, out var values))
            result.Add(key, values = new SortedSet<string>(StringComparer.Ordinal));
        values.Add(value);
    }

    private static string _name(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
    }

    private static string _memberName(ISymbol member)
    {
        return member is IParameterSymbol parameter
            ? parameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) + ":" + parameter.Name
            : member is IPropertySymbol { IsIndexer: true } property
                ? property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            : member.MetadataName;
    }

    private static string _key(ISymbol member)
    {
        return _name(member.ContainingType) + "\t" + _memberName(member);
    }

    private static string _escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r")
            .Replace("\n", "\\n").Replace("*/", "*\\/").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
    }

    private static void _verify(SourceProductionContext context, Surface surface, ImmutableArray<string?> files)
    {
        if (files.Length == 0 && surface.Entries.Count == 0)
            return;
        if (files.Length != 1 || files[0] is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None,
                files.Length > 1 ? "multiple baselines supplied" : "baseline missing or unreadable"));
            return;
        }
        if (!_parse(files[0]!, out var baseline))
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None, "baseline is malformed"));
            return;
        }
        foreach (var item in surface.Entries.Values
            .Select(entry =>
            {
                var found = baseline.TryGetValue(entry.Key, out var previous);
                return (entry, found, previous);
            })
            .Where(static item => !(item.found && item.previous is not null && item.previous.Line == item.entry.Line)))
        {
            var entry = item.entry;
            var previous = item.previous;
            context.ReportDiagnostic(Diagnostic.Create(_drift, entry.Location, entry.Key.Replace('\t', '.')));
            if (previous is null || previous.Classifications.Any(old => !entry.Classifications.Any(current => _covers(current, old))))
                context.ReportDiagnostic(Diagnostic.Create(_weakened, entry.Location, entry.Key.Replace('\t', '.')));
        }
        foreach (var previous in baseline.Values.Where(previous => !surface.Entries.ContainsKey(previous.Key)))
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None, previous.Key.Replace('\t', '.')));
            if (surface.Members.Contains(previous.Key))
                context.ReportDiagnostic(Diagnostic.Create(_weakened, Location.None, previous.Key.Replace('\t', '.')));
        }
    }

    private static bool _covers(string current, string previous)
    {
        return current == previous
            || (previous == "Ark:Pseudonymous" && current is "Ark:PersonalData" or "Ark:SensitivePersonalData")
            || (previous == "Ark:PersonalData" && current == "Ark:SensitivePersonalData");
    }

    private static bool _parse(string text, out Dictionary<string, Entry> entries)
    {
        entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        text = text.Replace("\r\n", "\n").TrimStart('\uFEFF').TrimEnd('\n');
        if (text.StartsWith("/*\n", StringComparison.Ordinal) && text.EndsWith("\n*/", StringComparison.Ordinal))
            text = text.Substring(3, text.Length - 6).TrimEnd('\n');
        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].TrimStart('\uFEFF') != Header)
            return false;
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split('\t');
            if (fields.Length != 6 || fields[0] != "CLASSIFIED" || fields[1].Length == 0 || fields[2].Length == 0)
                return false;
            var classifications = fields[3].Split(',');
            if (classifications.Any(static classification => classification is not
                    ("Ark:PersonalData" or "Ark:SensitivePersonalData" or "Ark:Secret" or "Ark:Pseudonymous")))
                return false;
            var key = fields[1] + "\t" + fields[2];
            if (entries.ContainsKey(key))
                return false;
            entries.Add(key, new Entry(key, line, classifications.ToImmutableArray(), Location.None));
        }
        return true;
    }

    private sealed class Entry
    {
        internal Entry(string key, string line, ImmutableArray<string> classifications, Location location)
        {
            Key = key;
            Line = line;
            Classifications = classifications;
            Location = location;
        }

        internal string Key { get; }
        internal string Line { get; }
        internal ImmutableArray<string> Classifications { get; }
        internal Location Location { get; }
    }

    private sealed class Surface
    {
        internal Surface(string text, SortedDictionary<string, Entry> entries, HashSet<string> members)
        {
            Text = text;
            Entries = entries;
            Members = members;
        }

        internal string Text { get; }
        internal SortedDictionary<string, Entry> Entries { get; }
        internal HashSet<string> Members { get; }
    }
}
