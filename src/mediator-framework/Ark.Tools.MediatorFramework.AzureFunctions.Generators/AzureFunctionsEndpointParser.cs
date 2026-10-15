// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Ark.Tools.MediatorFramework.Generators;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>The mediator handler shape backing an HTTP endpoint.</summary>
internal enum HandlerKind
{
    /// <summary>The contract is not a mediator contract.</summary>
    None,

    /// <summary>The contract is a request.</summary>
    Request,

    /// <summary>The contract is a query.</summary>
    Query,

    /// <summary>The contract is a command.</summary>
    Command
}

/// <summary>A symbol-free description of a bindable endpoint property.</summary>
/// <param name="Name">The property name.</param>
/// <param name="TypeFullName">The fully qualified property type.</param>
/// <param name="IsRoute">Whether the property binds from the route.</param>
/// <param name="BindingName">The binding name used for route values.</param>
/// <param name="IsQuery">Whether the property binds from the query string.</param>
/// <param name="IsBody">Whether the property binds from the body.</param>
/// <param name="IsString">Whether the property type is <see cref="string"/>.</param>
/// <param name="IsETag">Whether the property carries the request ETag.</param>
/// <param name="IsAttachment">Whether the property is a single attachment.</param>
/// <param name="IsAttachmentCollection">Whether the property is an attachment collection.</param>
/// <param name="IsStringCollection">Whether the property type is one of the supported string collection shapes that receive every value of a query parameter.</param>
/// <param name="IsNotConvertible">Whether the property cannot bind from its route value or query string, by the rules Minimal API applies.</param>
/// <param name="ArrayElementTypeFullName">The fully qualified element type of an array, other than <c>string[]</c>, bound from every value of a query parameter; otherwise <see langword="null"/>.</param>
/// <param name="Conversion">How one value converts to the property type, or to the array element type.</param>
/// <param name="ConversionTypeFullName">The fully qualified type one value converts to: the property or element type without <c>Nullable&lt;T&gt;</c>.</param>
/// <param name="ParserTypeFullName">The fully qualified type that declares the <c>TryParse</c> method to call, which can be a base type; otherwise the conversion type.</param>
/// <param name="IsNullableTarget">Whether the property or element type is a <c>Nullable&lt;T&gt;</c> or an annotated nullable reference type, so an empty array element, or an absent query value, is <see langword="null"/>.</param>
/// <param name="Location">The property declaration location, when it is in source.</param>
internal readonly record struct PropertySpec(
    string Name,
    string TypeFullName,
    bool IsRoute,
    string BindingName,
    bool IsQuery,
    bool IsBody,
    bool IsString,
    bool IsETag,
    bool IsAttachment,
    bool IsAttachmentCollection,
    bool IsStringCollection,
    bool IsNotConvertible,
    string? ArrayElementTypeFullName,
    ConversionKind Conversion,
    string ConversionTypeFullName,
    string ParserTypeFullName,
    bool IsNullableTarget,
    LocationSpec? Location);

/// <summary>A symbol-free description of an HTTP endpoint contract.</summary>
/// <param name="TypeName">The flattened contract type name.</param>
/// <param name="FullyQualifiedType">The fully qualified contract type.</param>
/// <param name="AssemblyName">The assembly declaring the contract.</param>
/// <param name="Verb">The HTTP verb.</param>
/// <param name="Route">The expanded route.</param>
/// <param name="FunctionName">The generated function name.</param>
/// <param name="MessagePack">Whether the contract accepts MessagePack.</param>
/// <param name="Location">The contract declaration location.</param>
/// <param name="Prefix">The host version prefix.</param>
/// <param name="Template">The route template.</param>
/// <param name="Introduced">The first supported version.</param>
/// <param name="Retired">The first unsupported version, or zero.</param>
/// <param name="Kind">The mediator handler shape.</param>
/// <param name="ResponseType">The fully qualified response type.</param>
/// <param name="Properties">The bindable properties.</param>
/// <param name="AllowAnonymous">Whether the endpoint allows anonymous calls.</param>
/// <param name="SuccessStatusCode">The success status code.</param>
/// <param name="NullResultStatusCode">The status code used for null results, or zero.</param>
/// <param name="ResponseETagProperty">The response property carrying the ETag.</param>
/// <param name="BodyProperty">The property bound from the body.</param>
/// <param name="BodyType">The fully qualified body type.</param>
/// <param name="MaxFileCount">The maximum accepted file count, or zero.</param>
/// <param name="MaxRequestBodySizeBytes">The maximum accepted body size, or zero.</param>
/// <param name="AllowedContentTypes">The accepted content types.</param>
/// <param name="IsStreaming">Whether the response is streamed.</param>
/// <param name="IsRecord">Whether the contract is a record.</param>
/// <param name="ConstructorParameters">The constructor parameters used for binding.</param>
/// <param name="UnsettableProperties">The route or query properties without a public setter or init accessor, which the function cannot set.</param>
internal readonly record struct EndpointSpec(
    string TypeName,
    string FullyQualifiedType,
    string AssemblyName,
    string Verb,
    string Route,
    string FunctionName,
    bool MessagePack,
    LocationSpec? Location,
    string Prefix,
    string Template,
    int Introduced,
    int Retired,
    HandlerKind Kind,
    string ResponseType,
    EquatableArray<PropertySpec> Properties,
    bool AllowAnonymous,
    int SuccessStatusCode,
    int NullResultStatusCode,
    string? ResponseETagProperty,
    string? BodyProperty,
    string? BodyType,
    int MaxFileCount,
    long MaxRequestBodySizeBytes,
    EquatableArray<string> AllowedContentTypes,
    bool IsStreaming,
    bool IsRecord,
    EquatableArray<string> ConstructorParameters,
    EquatableArray<UnsettablePropertySpec> UnsettableProperties);

/// <summary>A route or query property the function cannot set.</summary>
/// <param name="Name">The property name.</param>
/// <param name="Location">The property declaration location, when it is in source.</param>
internal readonly record struct UnsettablePropertySpec(string Name, LocationSpec? Location);

/// <summary>A contract selection that does not belong to the host contract assembly.</summary>
/// <param name="TypeName">The selected type name.</param>
/// <param name="ListName">The selection list declaring the type.</param>
internal readonly record struct HostSelectionSpec(string TypeName, string ListName);

/// <summary>A symbol-free description of an HTTP host marker.</summary>
/// <param name="MarkerFullyQualifiedType">The fully qualified marker type.</param>
/// <param name="MarkerAssemblyName">The assembly declaring the marker.</param>
/// <param name="Prefix">The declared version prefix.</param>
/// <param name="Included">The fully qualified included contracts.</param>
/// <param name="Excluded">The fully qualified excluded contracts.</param>
/// <param name="MarkerIsInSource">Whether the marker is declared in the compilation.</param>
/// <param name="Location">The host attribute location.</param>
/// <param name="InvalidSelections">The selections that are not host contracts.</param>
/// <param name="MetadataEndpoints">The endpoints declared by a referenced contract assembly.</param>
internal readonly record struct HostSpec(
    string MarkerFullyQualifiedType,
    string MarkerAssemblyName,
    string Prefix,
    EquatableArray<string> Included,
    EquatableArray<string> Excluded,
    bool MarkerIsInSource,
    LocationSpec? Location,
    EquatableArray<HostSelectionSpec> InvalidSelections,
    EquatableArray<EndpointSpec> MetadataEndpoints);

/// <summary>The endpoints declared by a referenced host contract assembly.</summary>
/// <param name="AssemblyName">The contract assembly name.</param>
/// <param name="Endpoints">The endpoints declared by the assembly.</param>
internal readonly record struct AssemblyEndpointsSpec(
    string AssemblyName,
    EquatableArray<EndpointSpec> Endpoints);

/// <summary>Parses Azure Functions HTTP hosts and endpoints into symbol-free specifications.</summary>
internal static class AzureFunctionsEndpointParser
{
    // ponytail: [GeneratedRegex] is not available for netstandard2.0 targets; static field compiles and caches once.
    private static readonly Regex _routeParamRegex = new(@"\{(?<param>[^}:]+)(?::[^}]+)?\}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private const string _arkAttachment = "Ark.Tools.MediatorFramework.IArkAttachment";
    private const string _asyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";
    // Keeps `?` on a nullable reference result: the generated ExecuteAsync<TQuery, TResult> call must match the contract.
    private static readonly SymbolDisplayFormat _annotatedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Reads every host marker declared by an assembly attribute list.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The host specifications.</returns>
    public static ImmutableArray<HostSpec> _readHosts(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var builder = ImmutableArray.CreateBuilder<HostSpec>(context.Attributes.Length);
        foreach (var host in context.Attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (host.ConstructorArguments.Length < 2
                || host.ConstructorArguments[0].Value is not INamedTypeSymbol marker
                || host.ConstructorArguments[1].Value is not string prefix)
                continue;

            var markerAssembly = marker.ContainingAssembly;
            var included = ContractSymbols.NamedTypes(host, "IncludedContracts");
            var excluded = ContractSymbols.NamedTypes(host, "ExcludedContracts");
            var markerIsInSource = marker.Locations.Any(static location => location.IsInSource);

            builder.Add(new HostSpec(
                marker.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                markerAssembly.Name,
                prefix,
                _fullyQualifiedNames(included),
                _fullyQualifiedNames(excluded),
                markerIsInSource,
                LocationSpec._from(host.ApplicationSyntaxReference),
                _invalidSelections(included, excluded, markerAssembly),
                EquatableArray<EndpointSpec>.Empty));
        }

        return builder.ToImmutable();
    }

    /// <summary>Reads the endpoints declared by referenced host contract assemblies.</summary>
    /// <param name="compilation">A compilation exposing the referenced assemblies.</param>
    /// <param name="assemblyNames">The referenced contract assembly names.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The endpoints declared by each referenced contract assembly.</returns>
    public static ImmutableArray<AssemblyEndpointsSpec> _readReferencedEndpoints(
        Compilation compilation,
        ImmutableArray<string> assemblyNames,
        CancellationToken cancellationToken)
    {
        var endpointAttribute = compilation.GetTypeByMetadataName(ContractSymbols.HttpEndpointAttribute);
        var builder = ImmutableArray.CreateBuilder<AssemblyEndpointsSpec>(assemblyNames.Length);
        foreach (var assemblyName in assemblyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, assemblyName, StringComparison.Ordinal));
            if (assembly is not null)
                builder.Add(new AssemblyEndpointsSpec(assemblyName, _readMetadataEndpoints(assembly, endpointAttribute, cancellationToken)));
        }

        return builder.ToImmutable();
    }

    /// <summary>Reads an endpoint contract declared in the compilation.</summary>
    /// <param name="type">The contract type.</param>
    /// <param name="attribute">The endpoint attribute.</param>
    /// <returns>The endpoint specification, or <see langword="null"/> when the contract is unusable.</returns>
    public static EndpointSpec? _readEndpoint(INamedTypeSymbol type, AttributeData attribute)
    {
        if (attribute.ConstructorArguments.ElementAtOrDefault(0).Value is not string verb
            || attribute.ConstructorArguments.ElementAtOrDefault(1).Value is not string template
            || string.IsNullOrWhiteSpace(verb)
            || string.IsNullOrWhiteSpace(template))
            return null;

        var versioning = type.GetAttributes()
            .FirstOrDefault(static item => item.AttributeClass._isFullName(ContractSymbols.VersioningAttribute));
        var introduced = ContractSymbols.NamedInt(versioning, "Introduced", 1);
        var retired = ContractSymbols.NamedInt(versioning, "Retired", 0);
        var messagePack = ContractSymbols.NamedBool(attribute, "AcceptsMessagePack");
        var successStatusCode = ContractSymbols.NamedInt(attribute, "SuccessStatusCode", 200);
        var nullResultStatusCode = ContractSymbols.NamedInt(attribute, "NullResultStatusCode", 0);
        var maxFileCount = ContractSymbols.NamedInt(attribute, "MaxFileCount", 0);
        var maxRequestBodySizeBytes = ContractSymbols.NamedLong(attribute, "MaxRequestBodySizeBytes", 0);
        var allowedContentTypes = ContractSymbols.NamedStrings(attribute, "AllowedContentTypes");
        var kind = HandlerKind.None;
        string? responseType = null;
        INamedTypeSymbol? responseSymbol = null;
        foreach (var iface in type.AllInterfaces)
        {
            var definition = iface.OriginalDefinition;
            if (definition._isMetadataName("Ark.Tools.Solid", "IRequest`1"))
            {
                kind = HandlerKind.Request;
                responseType = iface.TypeArguments[0].ToDisplayString(_annotatedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition._isMetadataName("Ark.Tools.Solid", "IQuery`1"))
            {
                kind = HandlerKind.Query;
                responseType = iface.TypeArguments[0].ToDisplayString(_annotatedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition._isFullName("Ark.Tools.Solid.ICommand"))
            {
                kind = HandlerKind.Command;
                break;
            }
        }
        if (kind == HandlerKind.None)
            return null;

        // Extract route parameter names from the template
        var routeNames = new HashSet<string>(
            _routeParamRegex.Matches(template!)
                .Cast<Match>()
                .Select(static m => m.Groups["param"].Value)
                .Where(static n => !string.Equals(n, "version", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        // Extract per-property binding info at generation time (no runtime reflection per request)
        var properties = _allProperties(type)
            .Where(static p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic
                && p.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            .Select(p =>
            {
                var routeAttr = p.GetAttributes()
                    .FirstOrDefault(static a => a.AttributeClass._isFullName(ContractSymbols.HttpRouteAttribute));
                var bindingName = routeAttr?.ConstructorArguments.FirstOrDefault().Value as string ?? p.Name;
                var isRoute = routeAttr is not null || routeNames.Contains(p.Name);
                var isQuery = p.GetAttributes().Any(static a => a.AttributeClass._isFullName(ContractSymbols.HttpQueryAttribute));
                var isBody = p.GetAttributes().Any(static a => a.AttributeClass._isFullName(ContractSymbols.HttpBodyAttribute));
                var isETag = p.GetAttributes().Any(static a => a.AttributeClass._isFullName(ContractSymbols.ETagAttribute));
                var isString = p.Type.SpecialType == SpecialType.System_String;
                var isAttachment = _isArkAttachment(p.Type);
                var isAttachmentCollection = p.Type is INamedTypeSymbol collection
                    && collection.AllInterfaces.Any(static item => item._isMetadataName("System.Collections.Generic", "IEnumerable`1"))
                    && collection.TypeArguments.Length == 1
                    && _isArkAttachment(collection.TypeArguments[0]);
                // An array of string-bindable elements, other than string[], binds from every value of a query
                // parameter, one element per value, as Minimal API binds it natively.
                var element = p.Type is IArrayTypeSymbol { Rank: 1 } array
                    && array.ElementType.SpecialType != SpecialType.System_String
                    && HttpStringBinding.IsStringBindable(p.Type)
                    ? array.ElementType
                    : null;
                var conversionType = element ?? p.Type;
                return new PropertySpec(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    isRoute,
                    bindingName,
                    isQuery,
                    isBody,
                    isString,
                    isETag,
                    isAttachment,
                    isAttachmentCollection,
                    // A nullable StringValues receives every value too, as Minimal API binds it natively.
                    HttpStringBinding.IsStringCollection(HttpStringBinding.WithoutNullable(p.Type)),
                    isRoute ? !HttpStringBinding.CanBindFromRoute(p.Type) : !HttpStringBinding.CanBindExplicitly(p.Type),
                    element is null ? null : element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    HttpStringBinding.ConversionKindOf(conversionType),
                    HttpStringBinding.WithoutNullable(conversionType).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    HttpStringBinding.ParserType(conversionType).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    // As the Minimal API generator, which emits a reference type outside a nullable context as non-nullable.
                    conversionType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                        || (conversionType.IsReferenceType && conversionType.NullableAnnotation == NullableAnnotation.Annotated),
                    LocationSpec._from(p));
            })
            .ToImmutableArray();
        var responseETagProperty = responseSymbol is null
            ? null
            : _allProperties(responseSymbol)
                .FirstOrDefault(static property => property.GetAttributes().Any(static attribute =>
                    attribute.AttributeClass._isFullName(ContractSymbols.ETagAttribute)))
                ?.Name;
        var bodyProperty = properties.FirstOrDefault(static property => property.IsBody);

        return new EndpointSpec(
            _typeName(type),
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.ContainingAssembly.Name,
            verb.ToUpperInvariant(),
            string.Empty,
            string.Empty,
            messagePack,
            LocationSpec._from(type),
            string.Empty,
            template,
            Math.Max(1, introduced),
            retired,
            kind,
            responseType ?? "global::System.Void",
            properties,
            ContractSymbols.NamedBool(attribute, "AllowAnonymous"),
            successStatusCode,
            nullResultStatusCode,
            responseETagProperty,
            bodyProperty.Name,
            bodyProperty.Name is null ? type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : bodyProperty.TypeFullName,
            maxFileCount,
            maxRequestBodySizeBytes,
            allowedContentTypes,
            responseSymbol is { } responseNamed
                && responseNamed.OriginalDefinition.ToDisplayString() == _asyncEnumerable,
            type.IsRecord,
            _constructorParameters(type, properties),
            // The function sets bound properties after constructing the contract, so it cannot bind these.
            _allProperties(type)
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer
                    && p.SetMethod is not { DeclaredAccessibility: Accessibility.Public }
                    && (routeNames.Contains(p.Name) || p.GetAttributes().Any(static a =>
                        a.AttributeClass is { Name: "HttpRouteAttribute" or "HttpQueryAttribute" } attributeClass
                        && (attributeClass._isFullName(ContractSymbols.HttpRouteAttribute) || attributeClass._isFullName(ContractSymbols.HttpQueryAttribute)))))
                .Select(static p => new UnsettablePropertySpec(p.Name, LocationSpec._from(p)))
                .ToImmutableArray());
    }

    // Matches ToDisplayString() == _arkAttachment, which an annotated IArkAttachment? does not.
    private static bool _isArkAttachment(ITypeSymbol type)
        => type is INamedTypeSymbol { NullableAnnotation: not NullableAnnotation.Annotated } named && named._isFullName(_arkAttachment);

    private static EquatableArray<EndpointSpec> _readMetadataEndpoints(
        IAssemblySymbol assembly,
        INamedTypeSymbol? endpointAttribute,
        CancellationToken cancellationToken)
    {
        var builder = ImmutableArray.CreateBuilder<EndpointSpec>();
        if (endpointAttribute is null)
            return builder.ToImmutable();

        foreach (var type in _allTypes(assembly.GlobalNamespace, cancellationToken))
        {
            var attribute = type.GetAttributes()
                .FirstOrDefault(item => SymbolEqualityComparer.Default.Equals(item.AttributeClass, endpointAttribute));
            if (attribute is null)
                continue;

            if (_readEndpoint(type, attribute) is { } endpoint)
                builder.Add(endpoint);
        }

        return builder.ToImmutable();
    }

    private static EquatableArray<HostSelectionSpec> _invalidSelections(
        ImmutableArray<INamedTypeSymbol> included,
        ImmutableArray<INamedTypeSymbol> excluded,
        IAssemblySymbol markerAssembly)
    {
        var builder = ImmutableArray.CreateBuilder<HostSelectionSpec>();
        foreach (var (list, name) in new[] { (included, "IncludedContracts"), (excluded, "ExcludedContracts") })
        {
            foreach (var selection in list)
            {
                if (!SymbolEqualityComparer.Default.Equals(selection.ContainingAssembly, markerAssembly)
                    || !selection.GetAttributes().Any(static attribute =>
                        attribute.AttributeClass is { Name: "HttpEndpointAttribute" } attributeClass
                        && attributeClass._isFullName(ContractSymbols.HttpEndpointAttribute)))
                {
                    builder.Add(new HostSelectionSpec(selection.Name, name));
                }
            }
        }

        return builder.ToImmutable();
    }

    private static EquatableArray<string> _fullyQualifiedNames(ImmutableArray<INamedTypeSymbol> types)
        => types.Select(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray();

    private static EquatableArray<string> _constructorParameters(
        INamedTypeSymbol type,
        ImmutableArray<PropertySpec> properties)
    {
        var propertyNames = properties.Select(static property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var constructor = type.InstanceConstructors
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public
                && candidate.Parameters.Length > 0
                && candidate.Parameters.All(parameter => propertyNames.Contains(parameter.Name)))
            .OrderByDescending(static candidate => candidate.Parameters.Length)
            .FirstOrDefault();
        return constructor is null
            ? ImmutableArray<string>.Empty
            : constructor.Parameters.Select(static parameter => parameter.Name).ToImmutableArray();
    }

    private static IEnumerable<INamedTypeSymbol> _allTypes(INamespaceSymbol space, CancellationToken cancellationToken)
    {
        foreach (var member in space.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is INamespaceSymbol child)
            {
                foreach (var type in _allTypes(child, cancellationToken))
                    yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in _allNestedTypes(type))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> _allNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in _allNestedTypes(nested))
                yield return child;
        }
    }

    private static IEnumerable<IPropertySymbol> _allProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
                yield return member;
    }

    private static string _typeName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType)
            names.Push(current.Name);
        return string.Join("_", names);
    }
}
