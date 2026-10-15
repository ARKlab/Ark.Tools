// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Generators;

public sealed partial class ComplianceSurfaceGenerator
{
    // What a value type contributes depends only on that type, and a few types (string, int, Guid...) back most
    // members: classify each named type once, without keeping its compilation alive.
    private static readonly ConditionalWeakTable<INamedTypeSymbol, string[]> _valueTypeClassifications = new();

    private static TypeSpec? _parseType(
        SemanticModel model,
        SyntaxNode node,
        ImmutableEquatableArray<string> baselineTypes,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (model.GetDeclaredSymbol(node, token) is not INamedTypeSymbol type)
            return null;

        string? typeName = null;
        var inBaseline = false;
        SortedSet<string>? typeClassifications = null;
        var members = new List<MemberSpec>();
        // Parameters of one method come in a row: format the method signature once for all of them.
        ISymbol? method = null;
        string? methodName = null;
        foreach (var member in _members(type))
        {
            token.ThrowIfCancellationRequested();
            // The type-level facts are hoisted out of the member loop: computing them per member
            // dominated generator time on large compilations.
            if (typeClassifications is null)
            {
                typeName = _name(type);
                inBaseline = baselineTypes.Items.BinarySearch(typeName, StringComparer.Ordinal) >= 0;
                typeClassifications = new SortedSet<string>(StringComparer.Ordinal);
                _classifications(type, typeClassifications);
            }

            if (member is IParameterSymbol { ContainingSymbol: var containing } && !ReferenceEquals(containing, method))
            {
                method = containing;
                methodName = containing.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            }

            var spec = _parseMember(member, type, typeName!, inBaseline, typeClassifications, methodName, token);
            if (spec is not null)
                members.Add(spec);
        }

        return members.Count == 0
            ? null
            : new TypeSpec(
                typeName!,
                new ImmutableEquatableArray<MemberSpec>(
                    members.OrderBy(static member => member.Key, StringComparer.Ordinal).ToImmutableArray()));
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

    private static MemberSpec? _parseMember(
        ISymbol member,
        INamedTypeSymbol type,
        string typeName,
        bool inBaseline,
        SortedSet<string> typeClassifications,
        string? methodName,
        CancellationToken token)
    {
        // Most members carry no classification: build the set only when one of the sources has any.
        SortedSet<string>? classifications = null;
        if (member.GetAttributes().Length > 0 || member is IPropertySymbol { OverriddenProperty: not null })
        {
            classifications = new SortedSet<string>(StringComparer.Ordinal);
            _classifications(member, classifications);
        }

        var valueType = _valueType(member);
        var valueClassifications = _valueTypeClassificationsOf(valueType);
        if (typeClassifications.Count > 0 || valueClassifications.Length > 0)
        {
            classifications ??= new SortedSet<string>(StringComparer.Ordinal);
            classifications.UnionWith(typeClassifications);
            classifications.UnionWith(valueClassifications);
        }

        classifications?.Remove("Ark:InfrastructureSecret");
        // Outside baseline types an unclassified member cannot be a weakened baseline entry.
        if (!inBaseline && (classifications is null || classifications.Count == 0))
            return null;

        var name = _memberName(member, methodName);
        var key = typeName + "\t" + name;
        if (classifications is null || classifications.Count == 0)
        {
            return new MemberSpec(
                key,
                name,
                ImmutableEquatableArray<string>.Empty,
                null,
                false,
                ImmutableEquatableArray<string>.Empty,
                ImmutableEquatableArray<string>.Empty,
                ImmutableEquatableArray<string>.Empty,
                false,
                default);
        }

        var serializerAttributes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var attribute in member.GetAttributes().Concat(type.GetAttributes()))
        {
            var serializer = _serializerAttribute(attribute.AttributeClass);
            if (serializer is not null)
                serializerAttributes.Add(serializer);
        }

        var ignoredSerializers = new SortedSet<string>(StringComparer.Ordinal);
        if (_hasAttribute(member, "System.Text.Json.Serialization.JsonIgnoreAttribute", alwaysOnly: true))
            ignoredSerializers.Add("System.Text.Json");
        if (_hasAttribute(member, "Newtonsoft.Json.JsonIgnoreAttribute"))
            ignoredSerializers.Add("Newtonsoft.Json");
        if (_hasAttribute(member, "ProtoBuf.ProtoIgnoreAttribute"))
            ignoredSerializers.Add("Protobuf");
        if (_hasAttribute(member, "MessagePack.IgnoreMemberAttribute"))
            ignoredSerializers.Add("MessagePack");

        var valueTypeNames = new SortedSet<string>(StringComparer.Ordinal);
        var hasSensitiveValueObject = _valueTypeFacts(
            valueType,
            valueTypeNames,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        return new MemberSpec(
            key,
            name,
            new ImmutableEquatableArray<string>(classifications.ToImmutableArray()),
            _documentation(member, token),
            _isSerializableMember(member),
            new ImmutableEquatableArray<string>(serializerAttributes.ToImmutableArray()),
            new ImmutableEquatableArray<string>(ignoredSerializers.ToImmutableArray()),
            new ImmutableEquatableArray<string>(valueTypeNames.ToImmutableArray()),
            hasSensitiveValueObject,
            LocationSpec.Create(member.Locations.FirstOrDefault()));
    }

    private static void _classifications(ISymbol symbol, SortedSet<string> output)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null || attributeClass.Arity != 0 || attributeClass.ContainingType is not null)
                continue;
            var classification = attributeClass.Name switch
            {
                "PersonalDataAttribute" => "Ark:PersonalData",
                "SensitivePersonalDataAttribute" => "Ark:SensitivePersonalData",
                "UserCredentialsAttribute" => "Ark:UserCredentials",
                "InfrastructureSecretAttribute" or "SecretAttribute" => "Ark:InfrastructureSecret",
                "PseudonymousAttribute" => "Ark:Pseudonymous",
                _ => null,
            };
            if (classification is not null && _isNamespace(attributeClass.ContainingNamespace, "Ark.Tools.Compliance"))
                output.Add(classification);
        }
        if (symbol is IPropertySymbol { OverriddenProperty: { } overridden })
            _classifications(overridden, output);
        if (symbol is INamedTypeSymbol { BaseType: { } baseType })
            _classifications(baseType, output);
    }

    private static string? _serializerAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null || attributeClass.Arity != 0 || attributeClass.ContainingType is not null)
            return null;
        return attributeClass.Name switch
        {
            "JsonPropertyNameAttribute" or "JsonIncludeAttribute"
                when _isNamespace(attributeClass.ContainingNamespace, "System.Text.Json.Serialization") => "System.Text.Json",
            "JsonPropertyAttribute" when _isNamespace(attributeClass.ContainingNamespace, "Newtonsoft.Json") => "Newtonsoft.Json",
            "ProtoMemberAttribute" when _isNamespace(attributeClass.ContainingNamespace, "ProtoBuf") => "Protobuf",
            "KeyAttribute" when _isNamespace(attributeClass.ContainingNamespace, "MessagePack") => "MessagePack",
            _ => null,
        };
    }

    private static string[] _valueTypeClassificationsOf(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
        {
            if (type is null)
                return Array.Empty<string>();

            var output = new SortedSet<string>(StringComparer.Ordinal);
            _typeClassifications(type, output, null);
            return output.Count == 0 ? Array.Empty<string>() : output.ToArray();
        }

        return _valueTypeClassifications.GetValue(named, static key =>
        {
            var output = new SortedSet<string>(StringComparer.Ordinal);
            _typeClassifications(key, output, null);
            return output.Count == 0 ? Array.Empty<string>() : output.ToArray();
        });
    }

    private static void _typeClassifications(ITypeSymbol? type, SortedSet<string> output, HashSet<ITypeSymbol>? seen)
    {
        if (type is null || seen?.Add(type) == false)
            return;
        _classifications(type, output);
        if (type is IArrayTypeSymbol array)
        {
            seen ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default) { type };
            _typeClassifications(array.ElementType, output, seen);
        }
        if (type is INamedTypeSymbol named && named.TypeArguments.Length > 0)
        {
            seen ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default) { type };
            foreach (var argument in named.TypeArguments)
                _typeClassifications(argument, output, seen);
        }
    }

    private static string? _documentation(ISymbol symbol, CancellationToken token)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: token);
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        try
        {
            var summary = XElement.Parse(xml).Element("summary")?.Value;
            return string.IsNullOrWhiteSpace(summary)
                ? null
                : string.Join(" ", summary!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch (XmlException)
        {
            // The compiler reports malformed documentation; it must not crash the inventory.
            return null;
        }
    }

    private static bool _isRevealCandidate(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Reveal" },
            ArgumentList.Arguments.Count: 1,
        };
    }

    private static RevealSpec? _parseReveal(SemanticModel model, SyntaxNode node, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var invocation = (InvocationExpressionSyntax)node;
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var compliancePurposeType = model.Compilation.GetTypeByMetadataName(Prefix + "CompliancePurpose");
        var compliancePurposeCategoryType = model.Compilation.GetTypeByMetadataName(Prefix + "CompliancePurposeCategory");
        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol revealMethod
            || revealMethod.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(revealMethod.Parameters[0].Type, compliancePurposeType)
            || model.GetSymbolInfo(access.Expression, token).Symbol is not { } member)
        {
            return null;
        }

        var expression = invocation.ArgumentList.Arguments[0].Expression;
        var purpose = model.GetSymbolInfo(expression, token).Symbol;
        string? text = null;
        string? category = null;
        if (purpose is IPropertySymbol property
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, compliancePurposeType))
        {
            text = property.Name;
            category = _builtInCategory(property, token);
        }
        else if (expression is InvocationExpressionSyntax custom
            && purpose is IMethodSymbol { Name: "Custom" } customMethod
            && SymbolEqualityComparer.Default.Equals(customMethod.ContainingType, compliancePurposeType)
            && custom.ArgumentList.Arguments.Count == 2)
        {
            var constant = model.GetConstantValue(custom.ArgumentList.Arguments[0].Expression, token);
            if (constant.HasValue && constant.Value is string value)
                text = value;
            var categorySymbol = model.GetSymbolInfo(custom.ArgumentList.Arguments[1].Expression, token).Symbol;
            if (categorySymbol is IFieldSymbol categoryField
                && SymbolEqualityComparer.Default.Equals(categoryField.ContainingType, compliancePurposeCategoryType))
            {
                category = categoryField.Name;
            }
        }

        return new RevealSpec(
            _key(member),
            "Reveal: " + (text ?? "(dynamic purpose)") + " [" + (category ?? "(dynamic category)") + "]");
    }

    // Only the serializer shapes: Register<T>() or Register<T>(arg), and RegisterBuiltIn() or RegisterBuiltIn(arg).
    // DI calls such as container.Register<IFoo, Foo>() or container.Register(typeof(IFoo), ...) are never bound.
    private static bool _isRegistrationCandidate(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: <= 1 } invocation
            && _invocationSimpleName(invocation) switch
            {
                GenericNameSyntax { Identifier.ValueText: "Register", TypeArgumentList.Arguments.Count: 1 } => true,
                IdentifierNameSyntax { Identifier.ValueText: "RegisterBuiltIn" } => true,
                _ => false,
            };
    }

    private static RegistrationSpec? _parseRegistration(SemanticModel model, SyntaxNode node, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var invocation = (InvocationExpressionSyntax)node;
        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method)
            return null;

        var compilation = model.Compilation;
        var serializer = SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                compilation.GetTypeByMetadataName(Prefix + "Dapper.SensitiveValueDapper"))
            ? "Dapper"
            : SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                compilation.GetTypeByMetadataName(Prefix + "NewtonsoftJson.SensitiveValueNewtonsoftJson"))
                ? "Newtonsoft.Json"
                : SymbolEqualityComparer.Default.Equals(
                    method.ContainingType,
                    compilation.GetTypeByMetadataName(Prefix + "Protobuf.SensitiveValueProtobuf"))
                    ? "Protobuf"
                    : SymbolEqualityComparer.Default.Equals(
                        method.ContainingType,
                        compilation.GetTypeByMetadataName(Prefix + "MessagePack.SensitiveValueFormatterResolver"))
                        ? "MessagePack"
                        : null;
        if (serializer is null)
            return null;

        var typeNames = method.Name == "RegisterBuiltIn"
            ? new ImmutableEquatableArray<string>(ImmutableArray.Create(
                Prefix + "EmailAddress",
                Prefix + "PhoneNumber",
                Prefix + "PersonName",
                Prefix + "PostalAddressLine",
                Prefix + "NationalIdentifier",
                Prefix + "ApiKey"))
            : new ImmutableEquatableArray<string>(method.TypeArguments.Select(_name).ToImmutableArray());
        return typeNames.Items.Length == 0 ? null : new RegistrationSpec(serializer, typeNames);
    }

    private static SimpleNameSyntax? _invocationSimpleName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            SimpleNameSyntax simpleName => simpleName,
            _ => null,
        };
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

    private static bool _valueTypeFacts(
        ITypeSymbol? type,
        SortedSet<string> typeNames,
        HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type))
            return false;

        typeNames.Add(_name(type));
        var hasSensitiveValueObject = false;
        foreach (var attribute in type.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is { Name: "SensitiveValueObjectAttribute", Arity: 1, ContainingType: null }
                && _isNamespace(attributeClass.ContainingNamespace, "Ark.Tools.Compliance"))
            {
                hasSensitiveValueObject = true;
                break;
            }
        }
        if (type is IArrayTypeSymbol array)
            hasSensitiveValueObject |= _valueTypeFacts(array.ElementType, typeNames, visited);
        if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
                hasSensitiveValueObject |= _valueTypeFacts(argument, typeNames, visited);
        }

        return hasSensitiveValueObject;
    }

    private static bool _hasAttribute(ISymbol symbol, string name, bool alwaysOnly = false)
    {
        return symbol.GetAttributes().Any(attribute => _isFullName(attribute.AttributeClass, name)
            && (!alwaysOnly || !attribute.NamedArguments.Any(static argument => argument.Key == "Condition")
                || attribute.NamedArguments.Any(static argument => argument.Key == "Condition" && argument.Value.Value is 1)));
    }

    /// <summary>
    /// Allocation-free equivalent of <c>type.ToDisplayString() == fullName</c> for top-level,
    /// non-generic types: generic or nested types render with type arguments or the containing
    /// type in their display string, so they can never equal a plain dotted name.
    /// </summary>
    internal static bool _isFullName(INamedTypeSymbol? type, string fullName)
    {
        if (type is null || type.Arity != 0 || type.ContainingType is not null)
            return false;

        var name = type.Name;
        var start = fullName.Length - name.Length;
        if (start <= 0
            || fullName[start - 1] != '.'
            || string.CompareOrdinal(fullName, start, name, 0, name.Length) != 0)
        {
            return false;
        }

        return _isNamespace(type.ContainingNamespace, fullName, start - 1);
    }

    /// <summary>Allocation-free equivalent of <c>ns.ToDisplayString() == dotted</c>.</summary>
    private static bool _isNamespace(INamespaceSymbol? @namespace, string dotted)
    {
        return _isNamespace(@namespace, dotted, dotted.Length);
    }

    private static bool _isNamespace(INamespaceSymbol? @namespace, string dotted, int end)
    {
        while (@namespace is { IsGlobalNamespace: false })
        {
            var name = @namespace.Name;
            var start = end - name.Length;
            if (start < 0
                || string.CompareOrdinal(dotted, start, name, 0, name.Length) != 0
                || (start > 0 && dotted[start - 1] != '.'))
            {
                return false;
            }

            end = start - 1;
            @namespace = @namespace.ContainingNamespace;
        }

        return end == -1 && @namespace is not null;
    }

    private static string _name(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
    }

    private static string _memberName(ISymbol member, string? methodName = null)
    {
        return member is IParameterSymbol parameter
            ? (methodName ?? parameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)) + ":" + parameter.Name
            : member is IPropertySymbol { IsIndexer: true } property
                ? property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            : member.MetadataName;
    }

    private static string _key(ISymbol member)
    {
        return _name(member.ContainingType) + "\t" + _memberName(member);
    }

    private sealed record TypeSpec(
        string Name,
        ImmutableEquatableArray<MemberSpec> Members);

    private sealed record MemberSpec(
        string Key,
        string Name,
        ImmutableEquatableArray<string> Classifications,
        string? Documentation,
        bool IsSerializable,
        ImmutableEquatableArray<string> SerializerAttributes,
        ImmutableEquatableArray<string> IgnoredSerializers,
        ImmutableEquatableArray<string> ValueTypeNames,
        bool HasSensitiveValueObject,
        LocationSpec Location);

    private readonly record struct RevealSpec(
        string MemberKey,
        string Note);

    private sealed record RegistrationSpec(
        string Serializer,
        ImmutableEquatableArray<string> TypeNames);

    private readonly record struct LocationSpec(
        string FilePath,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter)
    {
        internal static LocationSpec Create(Location? location)
        {
            if (location is null || !location.IsInSource)
                return default;

            var mappedLineSpan = location.GetMappedLineSpan();
            return new LocationSpec(
                string.IsNullOrEmpty(mappedLineSpan.Path)
                    ? location.SourceTree?.FilePath ?? string.Empty
                    : mappedLineSpan.Path,
                location.SourceSpan.Start,
                location.SourceSpan.Length,
                mappedLineSpan.StartLinePosition.Line,
                mappedLineSpan.StartLinePosition.Character,
                mappedLineSpan.EndLinePosition.Line,
                mappedLineSpan.EndLinePosition.Character);
        }

        internal Location ToLocation()
        {
            return string.IsNullOrEmpty(FilePath)
                ? Location.None
                : Location.Create(
                    FilePath,
                    new TextSpan(Start, Length),
                    new LinePositionSpan(
                        new LinePosition(StartLine, StartCharacter),
                        new LinePosition(EndLine, EndCharacter)));
        }
    }

    private readonly struct ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>
    {
        internal static ImmutableEquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

        internal ImmutableEquatableArray(ImmutableArray<T> items)
        {
            Items = items;
        }

        internal ImmutableArray<T> Items { get; }

        public bool Equals(ImmutableEquatableArray<T> other)
        {
            return Items.SequenceEqual(other.Items);
        }

        public override bool Equals(object? obj)
        {
            return obj is ImmutableEquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = 17;
            foreach (var item in Items)
                hash = unchecked((hash * 397) ^ EqualityComparer<T>.Default.GetHashCode(item!));
            return hash;
        }
    }
}
