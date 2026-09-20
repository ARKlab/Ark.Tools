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
/// <param name="IsServerSet">Whether the property is server set.</param>
/// <param name="IsString">Whether the property type is <see cref="string"/>.</param>
/// <param name="IsETag">Whether the property carries the request ETag.</param>
/// <param name="IsAttachment">Whether the property is a single attachment.</param>
/// <param name="IsAttachmentCollection">Whether the property is an attachment collection.</param>
internal readonly record struct PropertySpec(
    string Name,
    string TypeFullName,
    bool IsRoute,
    string BindingName,
    bool IsQuery,
    bool IsBody,
    bool IsServerSet,
    bool IsString,
    bool IsETag,
    bool IsAttachment,
    bool IsAttachmentCollection);

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
    EquatableArray<string> ConstructorParameters);

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

/// <summary>Parses Azure Functions HTTP hosts and endpoints into symbol-free specifications.</summary>
internal static class AzureFunctionsEndpointParser
{
    // ponytail: [GeneratedRegex] is not available for netstandard2.0 targets; static field compiles and caches once.
    private static readonly Regex _routeParamRegex = new(@"\{(?<param>[^}:]+)(?::[^}]+)?\}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    internal const string _endpointAttribute = "Ark.Tools.MediatorFramework.HttpEndpointAttribute";
    private const string _versioningAttribute = "Ark.Tools.MediatorFramework.VersioningAttribute";
    private const string _httpRouteAttribute = "Ark.Tools.MediatorFramework.HttpRouteAttribute";
    private const string _httpQueryAttribute = "Ark.Tools.MediatorFramework.HttpQueryAttribute";
    private const string _httpBodyAttribute = "Ark.Tools.MediatorFramework.HttpBodyAttribute";
    private const string _serverSetAttribute = "Ark.Tools.MediatorFramework.ServerSetAttribute";
    private const string _eTagAttribute = "Ark.Tools.MediatorFramework.ETagAttribute";
    private const string _arkAttachment = "Ark.Tools.MediatorFramework.IArkAttachment";
    private const string _asyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";
    private const string _solidRequest = "global::Ark.Tools.Solid.IRequest<TResponse>";
    private const string _solidQuery = "global::Ark.Tools.Solid.IQuery<TResult>";
    private const string _solidCommand = "global::Ark.Tools.Solid.ICommand";

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
            var included = _getTypes(host, "IncludedContracts");
            var excluded = _getTypes(host, "ExcludedContracts");
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
                markerIsInSource
                    ? EquatableArray<EndpointSpec>.Empty
                    : _readMetadataEndpoints(markerAssembly, cancellationToken)));
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
            .FirstOrDefault(static item => item.AttributeClass?.ToDisplayString() == _versioningAttribute);
        var introduced = _getNamedInt(versioning, "Introduced", 1);
        var retired = _getNamedInt(versioning, "Retired", 0);
        var messagePack = _getNamedBool(attribute, "AcceptsMessagePack");
        var successStatusCode = _getNamedInt(attribute, "SuccessStatusCode", 200);
        var nullResultStatusCode = _getNamedInt(attribute, "NullResultStatusCode", 0);
        var maxFileCount = _getNamedInt(attribute, "MaxFileCount", 0);
        var maxRequestBodySizeBytes = _getNamedLong(attribute, "MaxRequestBodySizeBytes", 0);
        var allowedContentTypes = _getNamedStrings(attribute, "AllowedContentTypes");
        var kind = HandlerKind.None;
        string? responseType = null;
        INamedTypeSymbol? responseSymbol = null;
        foreach (var iface in type.AllInterfaces)
        {
            var definition = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (definition == _solidRequest)
            {
                kind = HandlerKind.Request;
                responseType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition == _solidQuery)
            {
                kind = HandlerKind.Query;
                responseType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                responseSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
            if (definition == _solidCommand)
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
                    .FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == _httpRouteAttribute);
                var bindingName = routeAttr?.ConstructorArguments.FirstOrDefault().Value as string ?? p.Name;
                var isRoute = routeAttr is not null || routeNames.Contains(p.Name);
                var isQuery = p.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == _httpQueryAttribute);
                var isBody = p.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == _httpBodyAttribute);
                var isServerSet = p.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == _serverSetAttribute);
                var isETag = p.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == _eTagAttribute);
                var isString = p.Type.SpecialType == SpecialType.System_String;
                var isAttachment = p.Type.ToDisplayString() == _arkAttachment;
                var isAttachmentCollection = p.Type is INamedTypeSymbol collection
                    && collection.AllInterfaces.Any(static item => item.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal))
                    && collection.TypeArguments.Length == 1
                    && collection.TypeArguments[0].ToDisplayString() == _arkAttachment;
                return new PropertySpec(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    isRoute,
                    bindingName,
                    isQuery,
                    isBody,
                    isServerSet,
                    isString,
                    isETag,
                    isAttachment,
                    isAttachmentCollection);
            })
            .ToImmutableArray();
        var responseETagProperty = responseSymbol is null
            ? null
            : _allProperties(responseSymbol)
                .FirstOrDefault(static property => property.GetAttributes().Any(static attribute =>
                    attribute.AttributeClass?.ToDisplayString() == _eTagAttribute))
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
            _getNamedBool(attribute, "AllowAnonymous"),
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
            _constructorParameters(type, properties));
    }

    private static EquatableArray<EndpointSpec> _readMetadataEndpoints(IAssemblySymbol assembly, CancellationToken cancellationToken)
    {
        var builder = ImmutableArray.CreateBuilder<EndpointSpec>();
        foreach (var type in _allTypes(assembly.GlobalNamespace, cancellationToken))
        {
            var attribute = type.GetAttributes()
                .FirstOrDefault(static item => item.AttributeClass?.ToDisplayString() == _endpointAttribute);
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
                    || !selection.GetAttributes().Any(static attribute => attribute.AttributeClass?.ToDisplayString() == _endpointAttribute))
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

    private static ImmutableArray<INamedTypeSymbol> _getTypes(AttributeData attribute, string name)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        if (argument.Kind != TypedConstantKind.Array)
            return ImmutableArray<INamedTypeSymbol>.Empty;
        return argument.Values
            .Where(static value => value.Value is INamedTypeSymbol)
            .Select(static value => (INamedTypeSymbol)value.Value!)
            .ToImmutableArray();
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

    private static int _getNamedInt(AttributeData? attribute, string name, int fallback)
    {
        if (attribute is null)
            return fallback;

        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Value is int number ? number : fallback;
    }

    private static long _getNamedLong(AttributeData? attribute, string name, long fallback)
    {
        if (attribute is null)
            return fallback;

        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Value is long number ? number : fallback;
    }

    private static bool _getNamedBool(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value.Value is true;
    }

    private static EquatableArray<string> _getNamedStrings(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Where(static item => item.Value is string).Select(static item => (string)item.Value!).ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }

    private static string _typeName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType)
            names.Push(current.Name);
        return string.Join("_", names);
    }
}
