// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that discovers <c>Ark.Tools.Solid</c> requests/queries decorated with
    /// <c>[HttpEndpoint]</c> and emits <c>MapArkEndpointsFromAssembly</c> inside a
    /// <c>partial ArkGeneratedEndpoints</c> class. Only the Minimal API transport is emitted by this
    /// generator; add <c>Ark.Tools.MediatorFramework.Rebus.Generators</c> for Rebus and
    /// <c>Ark.Tools.MediatorFramework.Grpc.Generators</c> for gRPC.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkMinimalApiEndpointGenerator : IIncrementalGenerator
    {
        private const string HttpEndpointAttribute = "Ark.Tools.MediatorFramework.HttpEndpointAttribute";
        private const string ArkGenerateMinimalApiForAssemblyAttribute = "Ark.Tools.MediatorFramework.MinimalApi.ArkGenerateMinimalApiForAssemblyAttribute";
        private const string HttpQueryAttribute = "Ark.Tools.MediatorFramework.HttpQueryAttribute";
        private const string HttpBodyAttribute = "Ark.Tools.MediatorFramework.HttpBodyAttribute";
        private const string HttpRouteAttribute = "Ark.Tools.MediatorFramework.HttpRouteAttribute";
        private const string ServerSetAttribute = "Ark.Tools.MediatorFramework.ServerSetAttribute";
        private const string ETagAttribute = "Ark.Tools.MediatorFramework.ETagAttribute";
        private const string RebusMessageAttribute = "Ark.Tools.MediatorFramework.RebusMessageAttribute";
        private const string ApiGroupAttribute = "Ark.Tools.MediatorFramework.ApiGroupAttribute";
        private const string VersioningAttribute = "Ark.Tools.MediatorFramework.VersioningAttribute";
        private const string ArkAttachment = "Ark.Tools.MediatorFramework.IArkAttachment";
        private const string Enumerable = "System.Collections.Generic.IEnumerable`1";
        private const string List = "System.Collections.Generic.List`1";
        private const string ReadOnlyList = "System.Collections.Generic.IReadOnlyList`1";
        private const string ReadOnlyCollection = "System.Collections.Generic.IReadOnlyCollection`1";
        private const string AsyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";
        private static readonly DiagnosticDescriptor MultipleAttachments = new DiagnosticDescriptor(
            "ARKMF001",
            "Only one attachment is supported",
            "HTTP endpoint '{0}' declares more than one IArkAttachment property",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor UnsupportedAttachmentCollection = new DiagnosticDescriptor(
            "ARKMF005",
            "Unsupported attachment collection",
            "HTTP endpoint '{0}' has attachment collection property '{1}' with an unsupported shape",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor ServerSetPropertyCannotBeReset = new DiagnosticDescriptor(
            "ARKMF002",
            "Server-set property cannot be reset",
            "HTTP endpoint '{0}' has server-set property '{1}' without an accessible setter",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor PossibleMassAssignment = new DiagnosticDescriptor(
            "ARKMF003",
            "Possible mass assignment",
            "HTTP endpoint '{0}' has property '{1}' that may be server-owned; mark it with [ServerSet] or suppress this warning",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor DuplicateOperationName = new DiagnosticDescriptor(
            "ARKMF016",
            "Duplicate operation name",
            "HTTP endpoints '{0}' and '{1}' resolve to the same operation name '{2}' in API version {3}",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor VersionPrefixMissingToken = new DiagnosticDescriptor(
            "ARKMF020",
            "Version prefix is missing the version token",
            "The version prefix must contain the '{version}' token",
            "Ark.Tools.MediatorFramework",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointMappings = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is InvocationExpressionSyntax,
                    static (syntaxContext, cancellationToken) =>
                        GetAssemblyMapping(syntaxContext, cancellationToken))
                .Where(static mapping => mapping is not null)
                .Select(static (mapping, _) => mapping!.Value);
            var endpointAssemblies = endpointMappings
                .SelectMany(static (mapping, _) => mapping.AssemblyNames)
                .Collect();
            var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                    HttpEndpointAttribute,
                    static (_, _) => true,
                    static (attributeContext, _) => ExtractSourceEndpoint(attributeContext))
                .Where(static endpoint => endpoint is not null)
                .Select(static (endpoint, _) => endpoint!.Value);
            var referencedEndpoints = context.CompilationProvider
                .Combine(endpointAssemblies)
                .SelectMany(static (pair, cancellationToken) =>
                    GetReferencedEndpoints(pair.Left, pair.Right, cancellationToken));

            var collected = sourceEndpoints.Collect()
                .Combine(referencedEndpoints.Collect())
                .Combine(endpointMappings.Collect());

            context.RegisterSourceOutput(
                collected,
                static (spc, pair) =>
                {
                    foreach (var mapping in pair.Right)
                    {
                        if (mapping.InvalidVersionPrefixLocation is not null)
                            spc.ReportDiagnostic(Diagnostic.Create(VersionPrefixMissingToken, mapping.InvalidVersionPrefixLocation));
                    }

                    Emit(spc, pair.Left.Left.AddRange(pair.Left.Right));
                });
        }

        private static void EmitResponseETagAssignment(StringBuilder sb, EndpointModel endpoint)
        {
            if (endpoint.ResponseETagProperty is null)
                return;

            sb.AppendLine("                var etagResult = global::Ark.Tools.MediatorFramework.MinimalApi.ArkETag.ApplyResponseETag(");
            sb.Append("                    httpContext, result.").Append(endpoint.ResponseETagProperty)
                .Append(", ").Append(endpoint.Verb == "GET" ? "true" : "false").AppendLine(");");
            sb.AppendLine("                if (etagResult is not null)");
            sb.AppendLine("                    return etagResult;");
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
            var http = context.Attributes[0];
            var compilation = context.SemanticModel.Compilation;
            return Extract(
                type,
                http,
                compilation.GetTypeByMetadataName(HttpQueryAttribute),
                compilation.GetTypeByMetadataName(HttpBodyAttribute),
                compilation.GetTypeByMetadataName(HttpRouteAttribute),
                compilation.GetTypeByMetadataName(ServerSetAttribute),
                compilation.GetTypeByMetadataName(ETagAttribute),
                compilation.GetTypeByMetadataName(ArkAttachment),
                compilation.GetTypeByMetadataName(RebusMessageAttribute),
                compilation.GetTypeByMetadataName(ApiGroupAttribute),
                compilation.GetTypeByMetadataName(Enumerable),
                compilation.GetTypeByMetadataName(AsyncEnumerable),
                compilation.GetTypeByMetadataName(List),
                compilation.GetTypeByMetadataName(ReadOnlyList),
                compilation.GetTypeByMetadataName(ReadOnlyCollection));
        }

        private static EndpointAssemblyMapping? GetAssemblyMapping(
            GeneratorSyntaxContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = (InvocationExpressionSyntax)context.Node;
            var method = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var genericName = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(name =>
                    name.Identifier.ValueText is "MapArkEndpointsFromAssembly" or "MapArkEndpoints");
            if (genericName is null || genericName.TypeArgumentList.Arguments.Count != 1)
                return null;

            var methodName = genericName.Identifier.ValueText;
            if ((method is null || !string.Equals(method.MetadataName, methodName, StringComparison.Ordinal))
                && !IsGeneratedEndpointInvocation(invocation, methodName, context.SemanticModel, cancellationToken))
                return null;

            var assemblyNames = methodName == "MapArkEndpoints"
                ? GetContextAssemblyNames(context, genericName, cancellationToken)
                : GetAssemblyMarkerName(context, genericName, cancellationToken);
            if (assemblyNames.IsDefaultOrEmpty)
                return null;

            var versionPrefix = invocation.ArgumentList.Arguments
                .FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "versionPrefix")
                ?? (invocation.ArgumentList.Arguments.Count > 1 ? invocation.ArgumentList.Arguments[1] : null);
            var invalidVersionPrefixLocation = versionPrefix is not null
                && context.SemanticModel.GetConstantValue(versionPrefix.Expression) is { HasValue: true, Value: string prefix }
                && !prefix.Contains("{version}", StringComparison.OrdinalIgnoreCase)
                    ? versionPrefix.Expression.GetLocation()
                    : null;
            return new EndpointAssemblyMapping(assemblyNames, invalidVersionPrefixLocation);
        }

        private static ImmutableArray<string> GetAssemblyMarkerName(
            GeneratorSyntaxContext context,
            GenericNameSyntax genericName,
            CancellationToken cancellationToken)
        {
            return context.SemanticModel
                .GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken)
                .Type?.ContainingAssembly?.Name is { } assemblyName
                ? ImmutableArray.Create(assemblyName)
                : ImmutableArray<string>.Empty;
        }

        private static ImmutableArray<string> GetContextAssemblyNames(
            GeneratorSyntaxContext context,
            GenericNameSyntax genericName,
            CancellationToken cancellationToken)
        {
            if (context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken).Type
                is not INamedTypeSymbol contextType)
                return ImmutableArray<string>.Empty;

            return contextType.GetAttributes()
                .Where(attribute => attribute.AttributeClass?.ToDisplayString() == ArkGenerateMinimalApiForAssemblyAttribute)
                .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol)
                .Where(static marker => marker?.ContainingAssembly?.Name is not null)
                .Select(static marker => marker!.ContainingAssembly!.Name)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static bool IsGeneratedEndpointInvocation(
            InvocationExpressionSyntax invocation,
            string methodName,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (invocation.Expression is GenericNameSyntax directName)
                return string.Equals(directName.Identifier.ValueText, methodName, StringComparison.Ordinal);
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name is not GenericNameSyntax genericName
                || !string.Equals(genericName.Identifier.ValueText, methodName, StringComparison.Ordinal))
                return false;

            if (memberAccess.Expression.DescendantNodesAndSelf()
                .OfType<SimpleNameSyntax>()
                .Any(name => string.Equals(name.Identifier.ValueText, "ArkGeneratedEndpoints", StringComparison.Ordinal)))
                return true;

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type as INamedTypeSymbol;
            return receiverType is not null
                && (IsType(receiverType, "IEndpointRouteBuilder", "Microsoft.AspNetCore.Routing")
                    || receiverType.AllInterfaces.Any(item =>
                        IsType(item, "IEndpointRouteBuilder", "Microsoft.AspNetCore.Routing")));
        }

        private static ImmutableArray<EndpointModel> GetReferencedEndpoints(
            Compilation compilation,
            ImmutableArray<string> endpointAssemblies,
            CancellationToken cancellationToken)
        {
            var httpAttr = compilation.GetTypeByMetadataName(HttpEndpointAttribute);
            if (httpAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = httpAttr.ContainingAssembly;
            var httpQueryAttr = compilation.GetTypeByMetadataName(HttpQueryAttribute);
            var httpBodyAttr = compilation.GetTypeByMetadataName(HttpBodyAttribute);
            var httpRouteAttr = compilation.GetTypeByMetadataName(HttpRouteAttribute);
            var serverSetAttr = compilation.GetTypeByMetadataName(ServerSetAttribute);
            var etagAttr = compilation.GetTypeByMetadataName(ETagAttribute);
            var rebusMessageAttr = compilation.GetTypeByMetadataName(RebusMessageAttribute);
            var apiGroupAttr = compilation.GetTypeByMetadataName(ApiGroupAttribute);
            var attachmentType = compilation.GetTypeByMetadataName(ArkAttachment);
            var enumerableType = compilation.GetTypeByMetadataName(Enumerable);
            var asyncEnumerableType = compilation.GetTypeByMetadataName(AsyncEnumerable);
            var listType = compilation.GetTypeByMetadataName(List);
            var readOnlyListType = compilation.GetTypeByMetadataName(ReadOnlyList);
            var readOnlyCollectionType = compilation.GetTypeByMetadataName(ReadOnlyCollection);
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();
            var requestedAssemblies = endpointAssemblies.ToHashSet(StringComparer.Ordinal);

            foreach (var assembly in _referencedAssemblies(compilation, runtimeAssembly)
                .Where(assembly => requestedAssemblies.Contains(assembly.Name)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attrs = type.GetAttributes();
                    var http = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, httpAttr));
                    if (http is null)
                        continue;

                    var model = Extract(
                        type,
                        http,
                        httpQueryAttr,
                        httpBodyAttr,
                        httpRouteAttr,
                        serverSetAttr,
                        etagAttr,
                        attachmentType,
                        rebusMessageAttr,
                        apiGroupAttr,
                        enumerableType,
                        asyncEnumerableType,
                        listType,
                        readOnlyListType,
                        readOnlyCollectionType);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

        private static IEnumerable<IAssemblySymbol> _referencedAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols
                .Where(reference => !SymbolEqualityComparer.Default.Equals(reference, runtimeAssembly)))
            {
                var referencesRuntime = reference.Modules.Any(
                    m => m.ReferencedAssemblies.Any(
                        id => string.Equals(id.Name, runtimeAssembly.Name, StringComparison.Ordinal)));

                if (referencesRuntime)
                    yield return reference;
            }
        }

        private static IEnumerable<INamedTypeSymbol> _allTypes(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    foreach (var type in _allTypes(childNs))
                        yield return type;
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in AllNestedTypes(type))
                        yield return nested;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> AllNestedTypes(INamedTypeSymbol type)
        {
            foreach (var nested in type.GetTypeMembers())
            {
                yield return nested;
                foreach (var child in AllNestedTypes(nested))
                    yield return child;
            }
        }

        private static EndpointModel? Extract(
            INamedTypeSymbol type,
            AttributeData http,
            INamedTypeSymbol? httpQueryAttr,
            INamedTypeSymbol? httpBodyAttr,
            INamedTypeSymbol? httpRouteAttr,
            INamedTypeSymbol? serverSetAttr,
            INamedTypeSymbol? etagAttr,
            INamedTypeSymbol? attachmentType,
            INamedTypeSymbol? rebusMessageAttr,
            INamedTypeSymbol? apiGroupAttr,
            INamedTypeSymbol? enumerableType,
            INamedTypeSymbol? asyncEnumerableType,
            INamedTypeSymbol? listType,
            INamedTypeSymbol? readOnlyListType,
            INamedTypeSymbol? readOnlyCollectionType)
        {
            string? response = null;
            ITypeSymbol? responseType = null;
            string? streamElement = null;
            var attachmentResponse = false;
            var kind = HandlerKind.None;

            foreach (var iface in type.AllInterfaces)
            {
                var def = iface.OriginalDefinition;
                if (IsType(def, "IRequest`1", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Request;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    responseType = iface.TypeArguments[0];
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (IsType(def, "IQuery`1", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Query;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    responseType = iface.TypeArguments[0];
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (IsType(def, "ICommand", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Command;
                    break;
                }
            }

            var diagnostics = new List<DiagnosticInfo>();
            if (kind == HandlerKind.None || (kind != HandlerKind.Command && response is null))
            {
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.UnsupportedHandlerKind, type.Name, GetLocation(http)));
                return EndpointModel.Invalid(type, diagnostics);
            }

            if (http.ConstructorArguments.Length != 2)
                return EndpointModel.Invalid(type, diagnostics);

            var verb = http.ConstructorArguments[0].Value as string;
            var template = http.ConstructorArguments[1].Value as string;
            if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(template))
                return EndpointModel.Invalid(type, diagnostics);

            verb = verb!.ToUpperInvariant();
            if (verb is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH"))
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.UnknownHttpVerb, type.Name, GetLocation(http), verb));
            var versioning = type.GetAttributes().FirstOrDefault(
                candidate => IsAttribute(candidate, VersioningAttribute));
            var httpIntroducedIn = Version(type, "Introduced", 1);
            var httpRetiredIn = Version(type, "Retired", 0);
            var successStatusCode = NamedInt(http, "SuccessStatusCode", 0);
            var nullResultStatusCode = NamedInt(http, "NullResultStatusCode", 0);
            var acceptsMessagePack = NamedBool(http, "AcceptsMessagePack");
            var allowAnonymous = NamedBool(http, "AllowAnonymous");
            var requireAntiforgery = NamedBool(http, "RequireAntiforgery");
            var maxRequestBodySizeBytes = NamedLong(http, "MaxRequestBodySizeBytes");
            var maxFileCount = NamedInt(http, "MaxFileCount", 0);
            var maxStreamedItems = NamedInt(http, "MaxMessagePackStreamedItems", 0);
            var allowedContentTypes = NamedStringArray(http, "AllowedContentTypes");
            var ownerQueue = rebusMessageAttr is null
                ? null
                : type.GetAttributes()
                    .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, rebusMessageAttr))
                    .Select(attribute => NamedString(attribute, "OwnerQueue"))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var apiGroup = apiGroupAttr is null
                ? null
                : type.GetAttributes()
                    .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, apiGroupAttr))
                    .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var defaultTag = type.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString().Split('.').Last()
                : "Ark";
            var routeNames = new HashSet<string>(
                Regex.Matches(template!, "\\{([^}:]+)(?::[^}]+)?\\}")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Where(name => !string.Equals(name, "version", StringComparison.OrdinalIgnoreCase))
                , StringComparer.OrdinalIgnoreCase);
            var properties = AllProperties(type)
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                .Select(property =>
                {
                    var routeAttribute = httpRouteAttr is null
                        ? null
                        : property.GetAttributes().FirstOrDefault(attribute =>
                            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, httpRouteAttr));
                    var routeName = routeAttribute?.ConstructorArguments.FirstOrDefault().Value as string;
                    var isRoute = routeAttribute is not null || routeNames.Contains(property.Name);
                    return new PropertyModel(
                        property.Name,
                        property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        XmlDocumentation.Summary(property),
                        property.Type.SpecialType == SpecialType.System_String,
                        isRoute,
                        routeName
                            ?? routeNames.FirstOrDefault(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase))
                            ?? property.Name,
                        HasAttribute(property, httpQueryAttr),
                        serverSetAttr is not null && property.GetAttributes().Any(attribute =>
                            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serverSetAttr)),
                        etagAttr is not null && property.GetAttributes().Any(attribute =>
                            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, etagAttr)),
                        property.NullableAnnotation == NullableAnnotation.Annotated
                            || property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T },
                        property.SetMethod is not null && property.SetMethod.DeclaredAccessibility == Accessibility.Public,
                        IsStringCollection(property.Type, enumerableType),
                        !IsStringCollection(property.Type, enumerableType) && RequiresTypeConverterBinding(property.Type),
                        IsAttachmentCollection(property.Type, attachmentType, enumerableType, listType, readOnlyListType, readOnlyCollectionType),
                        IsAttachmentArray(property.Type, attachmentType),
                        httpBodyAttr is not null && HasAttribute(property, httpBodyAttr));
                })
                .ToImmutableArray();
            var bodyProperties = properties.Where(property => property.IsBody).ToImmutableArray();
            if (bodyProperties.Length > 1)
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidContractShape, type.Name, GetLocation(http)));
            var etagProperties = properties.Where(property => property.IsETag).ToArray();
            var responseETagProperties = responseType is INamedTypeSymbol namedResponse && etagAttr is not null
                ? AllProperties(namedResponse)
                    .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                    .Where(property => property.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, etagAttr)))
                    .ToArray()
                : Array.Empty<IPropertySymbol>();
            if (etagProperties.Length > 1)
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.DuplicateETagProperty, type.Name, GetLocation(http)));
            foreach (var property in etagProperties.Where(property => !property.IsString))
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidETagProperty, type.Name, GetLocation(http), property.Name));
            if (responseETagProperties.Length > 1)
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.DuplicateETagProperty, type.Name, GetLocation(http)));
            foreach (var property in responseETagProperties.Where(property => property.Type.SpecialType != SpecialType.System_String))
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidETagProperty, type.Name, GetLocation(http), property.Name));
            foreach (var routeName in routeNames)
            {
                if (!properties.Any(property => string.Equals(property.Name, routeName, StringComparison.OrdinalIgnoreCase)))
                    diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MissingRouteProperty, type.Name, GetLocation(http), routeName));
            }
            var bodyBinding = verb is not ("GET" or "DELETE");
            var hasInvalidBodyShape = bodyBinding && (!type.IsRecord || properties.Any(property => !property.HasPublicSetter));
            if (hasInvalidBodyShape)
                diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.InvalidContractShape, type.Name, GetLocation(http)));
            var attachmentProperties = attachmentType is null
                ? ImmutableArray<PropertyModel>.Empty
                : properties.Where(property => property.TypeFullName == "global::Ark.Tools.MediatorFramework.IArkAttachment" || property.IsAttachmentCollection).ToImmutableArray();
            foreach (var property in properties.Where(property => IsPotentialAttachmentCollection(property.TypeFullName))
                .Where(property => !property.IsAttachmentCollection))
                diagnostics.Add(new DiagnosticInfo(UnsupportedAttachmentCollection, type.Name, GetLocation(http), property.Name));

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GeneratedName(type),
                XmlDocumentation.Summary(type),
                XmlDocumentation.Remarks(type),
                apiGroup ?? defaultTag,
                verb,
                template!,
                versioning is not null,
                response ?? "global::System.Void",
                kind,
                httpIntroducedIn,
                httpRetiredIn,
                successStatusCode,
                nullResultStatusCode,
                acceptsMessagePack,
                allowAnonymous,
                requireAntiforgery,
                maxRequestBodySizeBytes,
                maxFileCount,
                maxStreamedItems,
                allowedContentTypes,
                ownerQueue,
                properties,
                bodyProperties.Length == 0 ? null : bodyProperties[0].Name,
                etagProperties.Length == 0 ? null : etagProperties[0].Name,
                responseETagProperties.Length == 0 ? null : responseETagProperties[0].Name,
                type.IsRecord,
                ConstructorParameters(type, properties),
                properties.Where(property => property.IsServerSet && !property.HasPublicSetter)
                    .Select(property => property.Name)
                    .ToImmutableArray(),
                properties.Where(property => !property.IsServerSet
                    && !property.IsQuery
                    && property.Name is "TenantId" or "UserId" or "IsAdmin" or "Role" or "Roles")
                    .Select(property => property.Name)
                    .ToImmutableArray(),
                attachmentProperties.Length,
                properties.Where(property => IsPotentialAttachmentCollection(property.TypeFullName))
                    .Where(property => !property.IsAttachmentCollection)
                    .Select(property => property.Name)
                    .ToImmutableArray(),
                attachmentResponse,
                streamElement,
                type.Locations.FirstOrDefault(),
                diagnostics);
        }

        private static bool HasAttribute(
            IPropertySymbol property,
            INamedTypeSymbol? attributeType)
            => property.GetAttributes().Any(attribute =>
                attributeType is not null && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));

        private static bool IsType(INamedTypeSymbol type, string metadataName, string namespaceName)
            => string.Equals(type.MetadataName, metadataName, StringComparison.Ordinal)
                && string.Equals(type.ContainingNamespace.ToDisplayString(), namespaceName, StringComparison.Ordinal);

        private static bool IsAttribute(AttributeData attribute, string metadataName)
            => attribute.AttributeClass is not null
                && string.Equals(attribute.AttributeClass.MetadataName, metadataName.Substring(metadataName.LastIndexOf('.') + 1), StringComparison.Ordinal)
                && string.Equals(
                    attribute.AttributeClass.ContainingNamespace.ToDisplayString(),
                    metadataName[..metadataName.LastIndexOf('.')], StringComparison.Ordinal);

        private static ImmutableArray<string> ConstructorParameters(
            INamedTypeSymbol type,
            ImmutableArray<PropertyModel> properties)
        {
            var propertyNames = properties.Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var constructor = type.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
                .Where(constructor => constructor.Parameters.Length > 0)
                .Where(constructor => constructor.Parameters.All(parameter => propertyNames.Contains(parameter.Name)))
                .OrderByDescending(constructor => constructor.Parameters.Length)
                .FirstOrDefault();
            return constructor is null
                ? ImmutableArray<string>.Empty
                : constructor.Parameters.Select(parameter => parameter.Name).ToImmutableArray();
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static int Version(INamedTypeSymbol type, string propertyName, int defaultValue)
        {
            var attribute = type.GetAttributes().FirstOrDefault(
                candidate => IsAttribute(candidate, VersioningAttribute));
            return attribute is null ? defaultValue : NamedInt(attribute, propertyName, defaultValue);
        }

        private static bool NamedBool(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is true;
        }

        private static string? NamedString(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value as string;
        }

        private static long NamedLong(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is long value ? value : 0;
        }

        private static ImmutableArray<string> NamedStringArray(AttributeData attribute, string name)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            if (argument.Value.Kind != TypedConstantKind.Array)
                return ImmutableArray<string>.Empty;

            return argument.Value.Values
                .Where(value => value.Value is string)
                .Select(value => (string)value.Value!)
                .ToImmutableArray();
        }

        private static bool IsAttachmentType(ITypeSymbol type, INamedTypeSymbol? attachmentType)
            => attachmentType is not null
                && (SymbolEqualityComparer.Default.Equals(type, attachmentType)
                    || type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, attachmentType)));

        private static bool IsAttachmentArray(ITypeSymbol type, INamedTypeSymbol? attachmentType)
            => type is IArrayTypeSymbol array && IsAttachmentType(array.ElementType, attachmentType);

        private static bool IsAttachmentCollection(
            ITypeSymbol type,
            INamedTypeSymbol? attachmentType,
            INamedTypeSymbol? enumerableType,
            INamedTypeSymbol? listType,
            INamedTypeSymbol? readOnlyListType,
            INamedTypeSymbol? readOnlyCollectionType)
        {
            if (type is IArrayTypeSymbol)
                return IsAttachmentArray(type, attachmentType);
            if (type is not INamedTypeSymbol named || !named.IsGenericType
                || !IsAttachmentType(named.TypeArguments[0], attachmentType))
                return false;
            return SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, enumerableType)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, listType)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, readOnlyListType)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, readOnlyCollectionType);
        }

        private static bool IsPotentialAttachmentCollection(string typeName)
            => typeName.Contains("IArkAttachment", StringComparison.Ordinal)
                && !string.Equals(typeName, "global::Ark.Tools.MediatorFramework.IArkAttachment", StringComparison.Ordinal);

        private static string? GetAsyncEnumerableElement(ITypeSymbol type, INamedTypeSymbol? asyncEnumerableType)
        {
            if (asyncEnumerableType is null || type is not INamedTypeSymbol named)
                return null;

            var match = named.AllInterfaces.Append(named)
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    asyncEnumerableType));
            return match?.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static void Emit(SourceProductionContext spc, ImmutableArray<EndpointModel> items)
        {
            if (items.IsDefaultOrEmpty)
                return;

            items = items.OrderBy(static item => item.TypeFullName, StringComparer.Ordinal).ToImmutableArray();
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.Tools.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated Minimal API transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.Tools.MediatorFramework.MinimalApi.Generators\", \"1.0.0\")]");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            // MapArkEndpointsFromAssembly is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every [HttpEndpoint]-declared handler to a Minimal API endpoint. TAssemblyMarker selects the assembly scanned for attributed contracts.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapArkEndpointsFromAssembly<TAssemblyMarker>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, global::System.Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null, string? versionPrefix = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            var group = endpoints.MapGroup(string.Empty);");
            sb.AppendLine("            var missingHandlers = new global::System.Collections.Generic.List<string>();");
            foreach (var handler in items
                .Where(static item => item.IsValid)
                .GroupBy(HandlerService, StringComparer.Ordinal)
                .Select(static group => (Handler: group.Key, Contract: group.First().TypeFullName)))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine("            VerifyMinimalApiHandlerRegistration(endpoints.ServiceProvider, typeof(" + handler.Handler + "), " + Literal(handler.Contract) + ", missingHandlers);");
            }
            sb.AppendLine("            if (missingHandlers.Count > 0)");
            sb.AppendLine("                throw new global::System.InvalidOperationException(\"Missing mediator handler registrations: \" + string.Join(\"; \", missingHandlers));");

            if (!items.IsDefaultOrEmpty)
            {
                var messagePackEndpoints = items
                    .Where(static endpoint => endpoint.AcceptsMessagePack)
                    .Select(static endpoint => endpoint.TypeFullName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (messagePackEndpoints.Length > 0)
                {
                    sb.Append("            global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackContracts(endpoints.ServiceProvider, ");
                    sb.Append(string.Join(
                        ", ",
                        messagePackEndpoints.Select(static type =>
                            "static resolver => global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackFormatter<"
                            + type + ">(resolver)")));
                    sb.AppendLine(");");
                }

                var maxVersion = items.Max(static x => Math.Max(x.HttpIntroducedIn, x.HttpRetiredIn > 0 ? x.HttpRetiredIn - 1 : 1));
                var operationGroups = new Dictionary<int, Dictionary<string, List<EndpointModel>>>();
                foreach (var endpoint in items.Where(static item => item.IsValid))
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    foreach (var version in ActiveVersions(endpoint, maxVersion))
                    {
                        if (!operationGroups.TryGetValue(version, out var groups))
                        {
                            groups = new Dictionary<string, List<EndpointModel>>(StringComparer.Ordinal);
                            operationGroups.Add(version, groups);
                        }

                        var operationName = OperationName(endpoint, version, maxVersion);
                        if (!groups.TryGetValue(operationName, out var endpoints))
                        {
                            endpoints = new List<EndpointModel>();
                            groups.Add(operationName, endpoints);
                        }

                        endpoints.Add(endpoint);
                    }
                }
                for (var version = 1; version <= maxVersion; version++)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    if (!operationGroups.TryGetValue(version, out var groups))
                        continue;

                    foreach (var duplicate in groups.Where(static group => group.Value.Count > 1))
                    {
                        var endpoints = duplicate.Value;
                        for (var index = 0; index < endpoints.Count; index++)
                        {
                            var other = endpoints[(index + 1) % endpoints.Count];
                            spc.ReportDiagnostic(Diagnostic.Create(
                                DuplicateOperationName,
                                endpoints[index].Location,
                                endpoints[index].TypeFullName,
                                other.TypeFullName,
                                duplicate.Key,
                                version));
                        }
                    }
                }
                var endpointIndex = 0;
                foreach (var e in items)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    var currentEndpointIndex = endpointIndex++;
                    foreach (var diagnostic in e.Diagnostics)
                        spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
                    if (!e.IsValid)
                        continue;
                    foreach (var property in e.InvalidServerSetProperties)
                        spc.ReportDiagnostic(Diagnostic.Create(ServerSetPropertyCannotBeReset, e.Location, e.TypeName, property));
                    foreach (var property in e.SuspiciousProperties)
                        spc.ReportDiagnostic(Diagnostic.Create(PossibleMassAssignment, e.Location, e.TypeName, property));

                    if (e.AttachmentCount > 1)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            MultipleAttachments,
                            e.Location,
                            e.TypeName));
                        continue;
                    }
                    foreach (var property in e.UnsupportedAttachmentCollections)
                        spc.ReportDiagnostic(Diagnostic.Create(UnsupportedAttachmentCollection, e.Location, e.TypeName, property));
                    if (e.UnsupportedAttachmentCollections.Length > 0)
                        continue;

                    var processorService = e.Kind == HandlerKind.Query
                        ? "global::Ark.Tools.Solid.IQueryProcessor"
                        : "global::Ark.Tools.Solid.IRequestProcessor";
                    var bind = (e.Verb == "GET" || e.Verb == "DELETE")
                        ? "[global::Microsoft.AspNetCore.Http.AsParameters] "
                        : string.Empty;
                    var bodyVerb = e.Verb != "GET" && e.Verb != "DELETE";
                    var explicitBindings = (e.Properties.Any(property => property.IsRoute || property.IsQuery)
                        || e.BodyProperty is not null)
                        && (bodyVerb || e.Verb == "GET" || e.Verb == "DELETE");

                    foreach (var version in ActiveVersions(e, maxVersion))
                    {
                        var map = MapMethod(e.Verb);
                        var templateVariable = "template" + currentEndpointIndex + "V" + version;
                        sb.AppendLine("            var " + templateVariable + " = VersionedRoute(versionPrefix, "
                            + Literal(e.Template) + ", " + e.IsVersioned.ToString().ToLowerInvariant() + ", " + version + ");");
                        if (e.Kind == HandlerKind.Command)
                        {
                            EmitCommandEndpoint(sb, e, map, templateVariable, version, maxVersion);
                            continue;
                        }
                        if (e.AttachmentCount == 1)
                        {
                            EmitMultipartEndpoint(sb, e, processorService, map, templateVariable, version, maxVersion);
                            continue;
                        }

                        if (e.AttachmentResponse)
                        {
                            EmitDownloadEndpoint(sb, e, processorService, map, templateVariable, version, maxVersion);
                            continue;
                        }

                        if (e.AcceptsMessagePack)
                        {
                            sb.AppendLine("            group." + map + "(" + templateVariable + ", static async (");
                            if (explicitBindings)
                            {
                                foreach (var property in e.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
                                {
                                    var source = property.IsRoute ? "FromRoute" : "FromQuery";
                                    var bindingName = property.IsRoute ? property.BindingName : property.Name;
                                    sb.AppendLine("                [global::Microsoft.AspNetCore.Mvc." + source + "(Name = " + Literal(bindingName) + ")] " + BindingType(property) + " " + property.Name + ",");
                                }
                            }
                            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
                            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
                            sb.AppendLine("            {");
                            sb.AppendLine("                " + BodyType(e) + "? body;");
                            sb.AppendLine("                try");
                            sb.AppendLine("                {");
                            sb.AppendLine("                    body = await global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ReadRequestAsync<" + BodyType(e) + ">(httpContext, cancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("                }");
                            sb.AppendLine("                catch (global::MessagePack.MessagePackSerializationException)");
                            sb.AppendLine("                {");
                            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
                            sb.AppendLine("                }");
                            sb.AppendLine("                catch (global::System.Text.Json.JsonException)");
                            sb.AppendLine("                {");
                            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
                            sb.AppendLine("                }");
                            sb.AppendLine("                if (body is null)");
                            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_REQUEST_BODY\", detail: \"Request body is missing or could not be deserialized.\");");
                            if (explicitBindings)
                            {
                                var assignments = string.Join(", ", e.Properties
                                    .Where(property => property.IsRoute || property.IsQuery)
                                    .Select(property => property.IsServerSet ? property.Name + " = default" : property.Name + " = " + BindingValue(property))
                                    .Concat(e.BodyProperty is null ? System.Linq.Enumerable.Empty<string>() : new[] { e.BodyProperty + " = body" })
                                    .Concat(e.ServerSetProperties.Where(property =>
                                        !e.Properties.Any(candidate =>
                                            candidate.Name == property && (candidate.IsRoute || candidate.IsQuery)))
                                        .Select(property => property + " = default")));
                                sb.AppendLine(e.BodyProperty is null
                                    ? "                var request = body with { " + assignments + " };"
                                    : "                var request = " + ConstructEnvelope(e, assignments) + ";");
                            }
                            else
                            {
                                if (e.IsRecord && e.ServerSetProperties.Length > 0)
                                    sb.AppendLine("                var request = body with { " + string.Join(", ", e.ServerSetProperties.Select(property => property + " = default")) + " };");
                                else
                                    sb.AppendLine("                var request = body;");
                            }
                            EmitServerSetAssignments(sb, e, "request");
                            EmitETagAssignment(sb, e);
                            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                            sb.AppendLine("                var processor = container.GetInstance<" + processorService + ">();");
                            sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, cancellationToken).ConfigureAwait(false);");
                            if (e.IsStreaming)
                            {
                                sb.AppendLine("                if (global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.PrefersMessagePackForGeneratedEndpoint(httpContext.Request.Headers.Accept))");
                                sb.AppendLine("                    return await global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.WriteStreamingResponseAsync<" + e.StreamElement + ">(httpContext, result, " + e.MaxMessagePackStreamedItems + ", cancellationToken, " + SuccessStatusCode(e) + ").ConfigureAwait(false);");
                                sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Json(global::Ark.Tools.MediatorFramework.MinimalApi.ArkStreaming.WithCancellation(result, cancellationToken), statusCode: " + SuccessStatusCode(e) + ");");
                            }
                            else
                            {
                                sb.AppendLine("                return global::Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.WriteResponse(httpContext, result, cancellationToken, "
                                    + SuccessStatusCode(e) + ", " + NullResultStatusCode(e) + ");");
                            }
                            var responseSchema = e.IsStreaming
                                ? "global::System.Collections.Generic.IEnumerable<" + e.StreamElement + ">"
                                : e.Response;
                            sb.AppendLine("            }).Accepts<" + BodyType(e) + ">(\"application/json\", \"application/x-msgpack\").Produces<" + responseSchema + ">("
                                + SuccessStatusCode(e) + ", \"application/json\", \"application/x-msgpack\").Produces(" + NullResultStatusCode(e)
                                + ")" + ProblemMetadata(e) + OpenApiMetadata(e, version, maxVersion) + AuthorizationMetadata(e) + ";");
                            continue;
                        }
                        sb.AppendLine("            group." + map + "(" + templateVariable + ", static async (");
                        if (explicitBindings)
                        {
                            foreach (var property in e.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
                            {
                                var source = property.IsRoute ? "FromRoute" : "FromQuery";
                                var bindingName = property.IsRoute ? property.BindingName : property.Name;
                                sb.AppendLine("                [global::Microsoft.AspNetCore.Mvc." + source + "(Name = " + Literal(bindingName) + ")] " + BindingType(property) + " " + property.Name + ",");
                            }

                            if (bodyVerb)
                                sb.AppendLine("                " + BodyType(e) + " body,");
                        }
                        else
                        {
                            sb.AppendLine("                " + bind + e.TypeFullName + " request,");
                        }
                        sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
                        sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
                        sb.AppendLine("            {");
                        if (explicitBindings)
                        {
                            var assignments = string.Join(", ", e.Properties
                                .Where(property => property.IsRoute || property.IsQuery)
                                .Select(property => property.Name + " = " + BindingValue(property))
                                .Concat(e.BodyProperty is null ? System.Linq.Enumerable.Empty<string>() : new[] { e.BodyProperty + " = body" })
                                .Concat(e.ServerSetProperties.Select(property => property + " = default")));
                            if (bodyVerb)
                                sb.AppendLine(e.BodyProperty is null
                                    ? "                var request = body with { " + assignments + " };"
                                    : "                var request = " + ConstructEnvelope(e, assignments) + ";");
                            else
                                sb.AppendLine("                var request = " + ConstructEnvelope(e, assignments) + ";");
                        }
                        else if (e.IsRecord && e.ServerSetProperties.Length > 0)
                        {
                            sb.AppendLine("                request = request with { " + string.Join(", ", e.ServerSetProperties.Select(property => property + " = default")) + " };");
                        }
                        EmitServerSetAssignments(sb, e, "request");
                        EmitETagAssignment(sb, e);
                        sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                        sb.AppendLine("                var processor = container.GetInstance<" + processorService + ">();");
                        sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, cancellationToken).ConfigureAwait(false);");
                        if (e.IsStreaming)
                        {
                            sb.AppendLine("                return global::Ark.Tools.MediatorFramework.MinimalApi.ArkStreaming.WithCancellation(result, cancellationToken);");
                            sb.AppendLine("            }).Produces<global::System.Collections.Generic.IEnumerable<" + e.StreamElement + ">>(" + SuccessStatusCode(e) + ")"
                                + ProblemMetadata(e) + OpenApiMetadata(e, version, maxVersion) + AuthorizationMetadata(e) + ";");
                            continue;
                        }
                        sb.AppendLine("                if (result is null)");
                        sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)" + NullResult(e) + ";");
                        EmitResponseETagAssignment(sb, e);
                        sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)" + SuccessResult(e) + ";");
                        sb.AppendLine("            }).Produces<" + e.Response + ">(" + SuccessStatusCode(e) + ").Produces(" + NullResultStatusCode(e)
                            + ")" + ProblemMetadata(e) + OpenApiMetadata(e, version, maxVersion) + AuthorizationMetadata(e) + ";");
                    }
                }
            }

            sb.AppendLine("            configure?.Invoke(group);");
            sb.AppendLine("            return group;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Maps contracts selected by ArkGenerateMinimalApiForAssemblyAttribute on TContext.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapArkEndpoints<TContext>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, global::System.Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null, string? versionPrefix = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            return MapArkEndpointsFromAssembly<TContext>(endpoints, configure, versionPrefix);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static string VersionedRoute(string? versionPrefix, string template, bool isVersioned, int version)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (template.Contains(\"{version}\", global::System.StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("                return template.Replace(\"{version}\", version.ToString(global::System.Globalization.CultureInfo.InvariantCulture), global::System.StringComparison.OrdinalIgnoreCase);");
            sb.AppendLine("            if (!isVersioned)");
            sb.AppendLine("                return template;");
            sb.AppendLine("            var prefixTemplate = versionPrefix ?? \"/api/v{version}\";");
            sb.AppendLine("            if (!prefixTemplate.Contains(\"{version}\", global::System.StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("                throw new global::System.ArgumentException(\"The version prefix must contain the '{version}' token.\", nameof(versionPrefix));");
            sb.AppendLine("            var prefix = prefixTemplate.TrimEnd('/').Replace(\"{version}\", version.ToString(global::System.Globalization.CultureInfo.InvariantCulture), global::System.StringComparison.OrdinalIgnoreCase);");
            sb.AppendLine("            return prefix + \"/\" + template.TrimStart('/');");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void VerifyMinimalApiHandlerRegistration(global::System.IServiceProvider services, global::System.Type handlerType, string contract, global::System.Collections.Generic.List<string> missingHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            var container = services.GetService(typeof(global::SimpleInjector.Container)) as global::SimpleInjector.Container;");
            sb.AppendLine("            if (container is not null ? container.GetRegistration(handlerType) is null : services.GetService(handlerType) is null)");
            sb.AppendLine("                missingHandlers.Add(contract + \" -> \" + handlerType);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.MinimalApi.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string HandlerService(EndpointModel item)
        {
            return item.Kind == HandlerKind.Query
                ? "global::Ark.Tools.Solid.IQueryHandler<" + item.TypeFullName + ", " + item.Response + ">"
                : item.Kind == HandlerKind.Command
                    ? "global::Ark.Tools.Solid.ICommandHandler<" + item.TypeFullName + ">"
                    : "global::Ark.Tools.Solid.IRequestHandler<" + item.TypeFullName + ", " + item.Response + ">";
        }

        private static string BindingType(PropertyModel property)
        {
            if (property.RequiresTypeConverterBinding)
            {
                var wrapper = "global::Ark.Tools.MediatorFramework.MinimalApi.ArkTypeConverterValue<"
                    + property.TypeFullName + ">";
                return property.IsNullable ? wrapper + "?" : wrapper;
            }

            return property.IsStringCollection
                ? "string[]"
                : property.TypeFullName switch
            {
                _ when property.IsNullable && property.TypeFullName is ("string" or "global::System.String") => "string?",
                _ => property.TypeFullName,
            };
        }

        private static string BodyType(EndpointModel endpoint)
        {
            return endpoint.BodyProperty is null
                ? endpoint.TypeFullName
                : endpoint.Properties.Single(property => property.Name == endpoint.BodyProperty).TypeFullName;
        }

        private static string GeneratedName(INamedTypeSymbol type)
        {
            var names = new Stack<string>();
            for (var current = type; current is not null; current = current.ContainingType)
                names.Push(current.Name);
            return string.Join("_", names);
        }

        private static string BindingValue(PropertyModel property)
        {
            if (property.RequiresTypeConverterBinding)
            {
                return property.IsNullable
                    ? property.Name + " is { } " + property.Name + "Value ? " + property.Name + "Value.Value : default"
                    : property.Name + ".Value";
            }

            if (!property.IsStringCollection)
                return property.Name;

            return property.TypeFullName switch
            {
                "global::System.Collections.Generic.List<string>"
                    or "global::System.Collections.Generic.IList<string>"
                    or "global::System.Collections.Generic.ICollection<string>"
                    => "new global::System.Collections.Generic.List<string>(" + property.Name + ")",
                "global::System.Collections.Generic.HashSet<string>"
                    or "global::System.Collections.Generic.ISet<string>"
                    => "new global::System.Collections.Generic.HashSet<string>(" + property.Name + ")",
                "global::System.Collections.Immutable.ImmutableArray<string>"
                    => "global::System.Collections.Immutable.ImmutableArray.CreateRange(" + property.Name + ")",
                _ => property.Name,
            };
        }

        private static bool IsStringCollection(ITypeSymbol type, INamedTypeSymbol? enumerableType)
        {
            return (type is IArrayTypeSymbol array && array.ElementType.SpecialType == SpecialType.System_String)
                || (enumerableType is not null
                    && ((type is INamedTypeSymbol named
                        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, enumerableType)
                        && named.TypeArguments[0].SpecialType == SpecialType.System_String)
                        || type.AllInterfaces.Any(iface =>
                            SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, enumerableType)
                            && iface.TypeArguments[0].SpecialType == SpecialType.System_String)));
        }

        private static IEnumerable<IPropertySymbol> AllProperties(INamedTypeSymbol type)
        {
            for (var current = type; current is not null; current = current.BaseType)
                foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                    yield return property;
        }

        private static bool RequiresTypeConverterBinding(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
                return false;

            var targetType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : type;
            if (targetType.SpecialType == SpecialType.System_String
                || targetType.TypeKind == TypeKind.Enum
                || targetType.ToDisplayString() is "System.Uri" or "Microsoft.Extensions.Primitives.StringValues")
                return false;

            return !targetType.GetMembers("TryParse")
                .OfType<IMethodSymbol>()
                .Any(method => method.IsStatic
                    && method.DeclaredAccessibility == Accessibility.Public
                    && method.ReturnType.SpecialType == SpecialType.System_Boolean
                    && method.Parameters.Length is 2 or 3
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_String
                    && (method.Parameters.Length == 2
                        || method.Parameters[1].Type.ToDisplayString() == "System.IFormatProvider")
                    && method.Parameters[^1].RefKind == RefKind.Out
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[^1].Type, targetType));
        }

        private static void EmitServerSetAssignments(StringBuilder sb, EndpointModel endpoint, string variable)
        {
            if (endpoint.IsRecord)
                return;

            foreach (var property in endpoint.ServerSetProperties)
                sb.Append("                ").Append(variable).Append('.').Append(property).AppendLine(" = default;");
        }

        private static void EmitETagAssignment(StringBuilder sb, EndpointModel endpoint)
        {
            if (endpoint.ETagProperty is null)
                return;

            sb.AppendLine("                var etag = global::Ark.Tools.MediatorFramework.MinimalApi.ArkETag.ReadPrecondition(httpContext);");
            if (endpoint.IsRecord)
                sb.AppendLine("                if (etag is not null) request = request with { " + endpoint.ETagProperty + " = etag };");
            else
                sb.AppendLine("                if (etag is not null) request." + endpoint.ETagProperty + " = etag;");
        }

        private static void EmitMultipartEndpoint(
            StringBuilder sb,
            EndpointModel endpoint,
            string processorService,
            string map,
            string templateExpression,
            int version,
            int maxVersion)
        {
            var attachment = endpoint.Properties.Single(property =>
                property.TypeFullName == "global::Ark.Tools.MediatorFramework.IArkAttachment" || property.IsAttachmentCollection);
            var bindings = endpoint.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet).ToArray();
            sb.Append("            group.").Append(map).Append("(").Append(templateExpression).AppendLine(", static async (");
            foreach (var property in bindings)
            {
                var source = property.IsRoute ? "FromRoute" : "FromQuery";
                var bindingName = property.IsRoute ? property.BindingName : property.Name;
                sb.Append("                [global::Microsoft.AspNetCore.Mvc.").Append(source)
                    .Append("(Name = ").Append(Literal(bindingName)).Append(")] ")
                    .Append(BindingType(property)).Append(' ').Append(property.Name).AppendLine(",");
            }

            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);");
            if (attachment.IsAttachmentCollection)
            {
                if (endpoint.MaxFileCount > 0)
                    sb.AppendLine("                if (form.Files.Count > " + endpoint.MaxFileCount + ")");
                else
                    sb.AppendLine("                if (false)");
                sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_FILE_COUNT\", detail: \"The number of uploaded files exceeds the configured limit of " + endpoint.MaxFileCount + ".\");");
                sb.AppendLine("                foreach (var file in form.Files)");
                sb.AppendLine("                {");
                EmitAllowedContentTypeCheck(sb, endpoint, "file");
                sb.AppendLine("                }");
            }
            else
            {
                sb.AppendLine("                if (form.Files.Count != 1)");
                sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.Problem(statusCode: 400, title: \"INVALID_FILE_COUNT\", detail: \"Exactly one file is required.\");");
                sb.AppendLine("                var file = form.Files[0];");
                EmitAllowedContentTypeCheck(sb, endpoint, "file");
            }
            sb.AppendLine("                var request = new " + endpoint.TypeFullName + " {");
            foreach (var property in bindings)
                sb.Append("                    ").Append(property.Name).Append(" = ").Append(BindingValue(property)).AppendLine(",");
            if (attachment.IsAttachmentCollection)
            {
                var conversion = attachment.IsAttachmentArray ? ".ToArray()" : ".ToList()";
                sb.AppendLine("                    " + attachment.Name + " = global::System.Linq.Enumerable.Select(form.Files, file => (global::Ark.Tools.MediatorFramework.IArkAttachment)new global::Ark.Tools.MediatorFramework.ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream))" + conversion + ",");
            }
            else
                sb.AppendLine("                    " + attachment.Name + " = new global::Ark.Tools.MediatorFramework.ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream),");
            sb.AppendLine("                };");
            EmitServerSetAssignments(sb, endpoint, "request");
            EmitETagAssignment(sb, endpoint);
            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
            sb.AppendLine("                var processor = container.GetInstance<" + processorService + ">();");
            sb.AppendLine("                var result = await processor.ExecuteAsync<" + endpoint.TypeFullName + ", " + endpoint.Response + ">(request, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                if (result is null)");
            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)" + NullResult(endpoint) + ";");
            EmitResponseETagAssignment(sb, endpoint);
            sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)" + SuccessResult(endpoint) + ";");
            sb.Append("            }).Accepts<").Append(attachment.IsAttachmentCollection ? "global::Microsoft.AspNetCore.Http.IFormFileCollection" : "global::Microsoft.AspNetCore.Http.IFormFile").Append(">(\"multipart/form-data\")").Append(ProblemMetadata(endpoint)).Append(OpenApiMetadata(endpoint, version, maxVersion)).Append(MultipartMetadata(endpoint))
                .Append(".Produces<").Append(endpoint.Response).Append(">(").Append(SuccessStatusCode(endpoint))
                .Append(").Produces(").Append(NullResultStatusCode(endpoint)).Append(')')
                .Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static void EmitAllowedContentTypeCheck(StringBuilder sb, EndpointModel endpoint, string fileVariable)
        {
            if (endpoint.AllowedContentTypes.IsDefaultOrEmpty)
                return;
            var allowedTypes = string.Join(", ", endpoint.AllowedContentTypes.Select(Literal));
            sb.AppendLine("                    if (!global::System.Linq.Enumerable.Contains(new[] { " + allowedTypes + " }, " + fileVariable + ".ContentType, global::System.StringComparer.OrdinalIgnoreCase))");
            sb.AppendLine("                        return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.StatusCode(415);");
        }

        private static void EmitDownloadEndpoint(
            StringBuilder sb,
            EndpointModel endpoint,
            string processorService,
            string map,
            string templateExpression,
            int version,
            int maxVersion)
        {
            var bindings = endpoint.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet).ToArray();
            sb.Append("            group.").Append(map).Append("(").Append(templateExpression).AppendLine(", static async (");
            foreach (var property in bindings)
            {
                var source = property.IsRoute ? "FromRoute" : "FromQuery";
                var bindingName = property.IsRoute ? property.BindingName : property.Name;
                sb.Append("                [global::Microsoft.AspNetCore.Mvc.").Append(source)
                    .Append("(Name = ").Append(Literal(bindingName)).Append(")] ")
                    .Append(BindingType(property)).Append(' ').Append(property.Name).AppendLine(",");
            }

            if (bindings.Length == 0)
                sb.AppendLine("                [global::Microsoft.AspNetCore.Http.AsParameters] " + endpoint.TypeFullName + " request,");
            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine("            {");
            if (bindings.Length > 0)
            {
                var assignments = string.Join(", ", bindings.Select(property => property.Name + " = " + BindingValue(property)));
                sb.AppendLine("                var request = new " + endpoint.TypeFullName + " { " + assignments + " };");
            }
            EmitServerSetAssignments(sb, endpoint, "request");
            EmitETagAssignment(sb, endpoint);
            sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
            sb.AppendLine("                var processor = container.GetInstance<" + processorService + ">();");
            sb.AppendLine("                var result = await processor.ExecuteAsync<" + endpoint.TypeFullName + ", " + endpoint.Response + ">(request, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                if (result is null)");
            sb.AppendLine("                    return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.TypedResults.NotFound();");
            sb.AppendLine("                return (global::Microsoft.AspNetCore.Http.IResult)global::Microsoft.AspNetCore.Http.Results.File(result.OpenRead(), result.ContentType, fileDownloadName: global::Ark.Tools.MediatorFramework.ArkAttachmentName.Sanitize(result.Name));");
            sb.Append("            }).Produces(200, contentType: \"application/octet-stream\").Produces(404)")
                .Append(ProblemMetadata(endpoint)).Append(OpenApiMetadata(endpoint, version, maxVersion)).Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static void EmitCommandEndpoint(
            StringBuilder sb,
            EndpointModel endpoint,
            string map,
            string templateExpression,
            int version,
            int maxVersion)
        {
            var bodyVerb = endpoint.Verb != "GET" && endpoint.Verb != "DELETE";
            var explicitBindings = bodyVerb && endpoint.Properties.Any(property => property.IsRoute || property.IsQuery);
            sb.Append("            group.").Append(map).Append("(").Append(templateExpression).AppendLine(", static async (");
            if (explicitBindings)
            {
                foreach (var property in endpoint.Properties.Where(property => (property.IsRoute || property.IsQuery) && !property.IsServerSet))
                {
                    var source = property.IsRoute ? "FromRoute" : "FromQuery";
                    var bindingName = property.IsRoute ? property.BindingName : property.Name;
                    sb.Append("                [global::Microsoft.AspNetCore.Mvc.").Append(source)
                        .Append("(Name = ").Append(Literal(bindingName)).Append(")] ")
                        .Append(BindingType(property)).Append(' ').Append(property.Name).AppendLine(",");
                }

                sb.AppendLine("                " + endpoint.TypeFullName + " body,");
            }
            else
            {
                sb.AppendLine("                " + (bodyVerb ? string.Empty : "[global::Microsoft.AspNetCore.Http.AsParameters] ") + endpoint.TypeFullName + " request,");
            }

            sb.AppendLine("                global::Microsoft.AspNetCore.Http.HttpContext httpContext,");
            sb.AppendLine("                global::System.Threading.CancellationToken cancellationToken) =>");
            sb.AppendLine("            {");
            if (explicitBindings)
            {
                var assignments = string.Join(", ", endpoint.Properties
                    .Where(property => property.IsRoute || property.IsQuery)
                    .Select(property => property.Name + " = " + BindingValue(property))
                    .Concat(endpoint.ServerSetProperties.Select(property => property + " = default")));
                sb.AppendLine("                var request = body with { " + assignments + " };");
            }
            else if (endpoint.IsRecord && endpoint.ServerSetProperties.Length > 0)
            {
                sb.AppendLine("                request = request with { " + string.Join(", ", endpoint.ServerSetProperties.Select(property => property + " = default")) + " };");
            }
            EmitServerSetAssignments(sb, endpoint, "request");
            if (endpoint.OwnerQueue is not null)
            {
                sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                sb.AppendLine("                var bus = container.GetInstance<global::Rebus.Bus.IBus>();");
                sb.AppendLine("                await bus.Advanced.Routing.Send(" + Literal(endpoint.OwnerQueue) + ", request).ConfigureAwait(false);");
                sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.StatusCode(202);");
            }
            else
            {
                sb.AppendLine("                var container = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::SimpleInjector.Container>(httpContext.RequestServices);");
                sb.AppendLine("                var processor = container.GetInstance<global::Ark.Tools.Solid.ICommandProcessor>();");
                sb.AppendLine("                await processor.ExecuteAsync<" + endpoint.TypeFullName + ">(request, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.NoContent();");
            }
            sb.Append("            })").Append(ProblemMetadata(endpoint)).Append(OpenApiMetadata(endpoint, version, maxVersion));
            sb.Append(endpoint.OwnerQueue is null ? ".Produces(204)" : ".Produces(202)");
            sb.Append(AuthorizationMetadata(endpoint)).AppendLine(";");
        }

        private static string MultipartMetadata(EndpointModel endpoint)
        {
            var metadata = new StringBuilder();
            if (!endpoint.RequireAntiforgery)
                metadata.Append(" /* Bearer-token API upload: antiforgery validation is intentionally disabled. */.DisableAntiforgery()");
            if (endpoint.MaxRequestBodySizeBytes > 0)
                metadata.Append(".WithMetadata(new global::Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(")
                    .Append(endpoint.MaxRequestBodySizeBytes)
                    .Append("L))");
            return metadata.ToString();
        }

        private static int SuccessStatusCode(EndpointModel endpoint)
            => endpoint.SuccessStatusCode == 0 ? 200 : endpoint.SuccessStatusCode;

        private static int NullResultStatusCode(EndpointModel endpoint)
            => endpoint.NullResultStatusCode == 0
                ? endpoint.Kind == HandlerKind.Query ? 404 : 204
                : endpoint.NullResultStatusCode;

        private static string NullResult(EndpointModel endpoint)
            => NullResultStatusCode(endpoint) switch
            {
                200 => "global::Microsoft.AspNetCore.Http.TypedResults.Ok()",
                204 => "global::Microsoft.AspNetCore.Http.TypedResults.NoContent()",
                404 => "global::Microsoft.AspNetCore.Http.TypedResults.NotFound()",
                var statusCode => "global::Microsoft.AspNetCore.Http.Results.StatusCode(" + statusCode + ")",
            };

        private static string SuccessResult(EndpointModel endpoint)
            => SuccessStatusCode(endpoint) == 200
                ? "global::Microsoft.AspNetCore.Http.TypedResults.Ok(result)"
                : "global::Microsoft.AspNetCore.Http.Results.Json(result, statusCode: " + SuccessStatusCode(endpoint) + ")";

        private static string AuthorizationMetadata(EndpointModel endpoint)
        {
            if (endpoint.AllowAnonymous)
                return ".AllowAnonymous()";

            return ".RequireAuthorization()";
        }

        private static string ProblemMetadata(EndpointModel endpoint)
        {
            var metadata = new StringBuilder();
            var declaredStatuses = new HashSet<int>(
                [SuccessStatusCode(endpoint), NullResultStatusCode(endpoint)]);

            AppendProblem(400);
            if (!endpoint.AllowAnonymous)
                AppendProblem(403);
            AppendProblem(500);
            return metadata.ToString();

            void AppendProblem(int statusCode)
            {
                if (declaredStatuses.Add(statusCode))
                    metadata.Append(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(")
                        .Append(statusCode)
                        .Append(", \"application/problem+json\")");
            }
        }

        private static string OpenApiMetadata(EndpointModel endpoint, int version, int maxVersion)
            => (endpoint.Summary is null && endpoint.Remarks is null && endpoint.Properties.All(property => property.Description is null)
                    ? string.Empty
                    : ".WithMetadata(new global::Ark.Tools.MediatorFramework.MinimalApi.ArkDocumentationMetadata("
                        + (endpoint.Summary is null ? "null" : Literal(endpoint.Summary)) + ", "
                        + (endpoint.Remarks is null ? "null" : Literal(endpoint.Remarks)) + ", "
                        + "new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.OrdinalIgnoreCase)"
                        + " { "
                        + string.Join(", ", endpoint.Properties
                            .Where(property => property.Description is not null)
                            .Select(property => " [" + Literal(property.Name) + "] = " + Literal(property.Description!)))
                        + " }))")
                + (endpoint.Summary is null ? string.Empty : ".WithSummary(" + Literal(endpoint.Summary) + ")")
                + (endpoint.Remarks is null ? string.Empty : ".WithDescription(" + Literal(endpoint.Remarks) + ")")
                + ".WithGroupName(" + Literal("v" + version)
                + ").WithTags(" + Literal(endpoint.ApiGroup)
                + ").WithName(" + Literal(OperationName(endpoint, version, maxVersion)) + ")"
                + (endpoint.ETagProperty is null && endpoint.ResponseETagProperty is null
                    ? string.Empty
                    : ".WithMetadata(new global::Ark.Tools.MediatorFramework.MinimalApi.ArkETagParameterMetadata("
                        + (endpoint.ETagProperty is null ? "false" : "true") + ", "
                        + (endpoint.ResponseETagProperty is null ? "false" : "true") + "))");

        private static string OperationName(EndpointModel endpoint, int version, int maxVersion)
            => ActiveVersions(endpoint, maxVersion).Count() > 1
                ? endpoint.TypeName + "_v" + version
                : endpoint.TypeName;

        private static string MapMethod(string verb) => verb switch
        {
            "GET" => "MapGet",
            "POST" => "MapPost",
            "PUT" => "MapPut",
            "DELETE" => "MapDelete",
            "PATCH" => "MapPatch",
            _ => throw new InvalidOperationException("Unknown HTTP verb should have been diagnosed before emission"),
        };

        private static IEnumerable<int> ActiveVersions(EndpointModel endpoint, int maxVersion)
        {
            for (var version = 1; version <= maxVersion; version++)
                if (version >= endpoint.HttpIntroducedIn
                    && (endpoint.HttpRetiredIn == 0 || version < endpoint.HttpRetiredIn))
                    yield return version;
        }

        private static string Literal(string value)
            => SyntaxFactory.Literal(value).ToFullString();

        private static string ConstructEnvelope(EndpointModel endpoint, string assignments)
        {
            if (endpoint.ConstructorParameters.IsDefaultOrEmpty)
                return "new " + endpoint.TypeFullName + " { " + assignments + " }";

            var values = assignments
                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(assignment =>
                {
                    var separator = assignment.IndexOf(" = ", StringComparison.Ordinal);
                    return separator < 0
                        ? (Name: string.Empty, Value: string.Empty)
                        : (Name: assignment[..separator], Value: assignment[(separator + 3)..]);
                })
                .Where(assignment => assignment.Name.Length > 0)
                .ToDictionary(assignment => assignment.Name, assignment => assignment.Value, StringComparer.OrdinalIgnoreCase);
            return "new " + endpoint.TypeFullName + "("
                + string.Join(", ", endpoint.ConstructorParameters.Select(parameter =>
                    values.TryGetValue(parameter, out var value) ? value : "default!"))
                + ")";
        }

        private enum HandlerKind
        {
            None = 0,
            Request = 1,
            Query = 2,
            Command = 3,
        }

        private readonly record struct EndpointAssemblyMapping(ImmutableArray<string> AssemblyNames, Location? InvalidVersionPrefixLocation);

        private readonly record struct EndpointModel
        {
            public EndpointModel(
                string typeFullName,
                string typeName,
                string? summary,
                string? remarks,
                string apiGroup,
                string verb,
                string template,
                bool isVersioned,
                string response,
                HandlerKind kind,
                int httpIntroducedIn,
                int httpRetiredIn,
                int successStatusCode,
                int nullResultStatusCode,
                bool acceptsMessagePack,
                bool allowAnonymous,
                bool requireAntiforgery,
                long maxRequestBodySizeBytes,
                int maxFileCount,
                int maxStreamedItems,
                ImmutableArray<string> allowedContentTypes,
                string? ownerQueue,
                ImmutableArray<PropertyModel> properties,
                string? bodyProperty,
                string? etagProperty,
                string? responseETagProperty,
                bool isRecord,
                ImmutableArray<string> constructorParameters,
                ImmutableArray<string> invalidServerSetProperties,
                ImmutableArray<string> suspiciousProperties,
                int attachmentCount,
                ImmutableArray<string> unsupportedAttachmentCollections,
                bool attachmentResponse,
                string? streamElement,
                Location? location,
                IReadOnlyList<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                Summary = summary;
                Remarks = remarks;
                ApiGroup = apiGroup;
                Verb = verb;
                Template = template;
                IsVersioned = isVersioned;
                Response = response;
                Kind = kind;
                HttpIntroducedIn = httpIntroducedIn;
                HttpRetiredIn = httpRetiredIn;
                SuccessStatusCode = successStatusCode;
                NullResultStatusCode = nullResultStatusCode;
                AcceptsMessagePack = acceptsMessagePack;
                AllowAnonymous = allowAnonymous;
                RequireAntiforgery = requireAntiforgery;
                MaxRequestBodySizeBytes = maxRequestBodySizeBytes;
                MaxFileCount = maxFileCount;
                MaxMessagePackStreamedItems = maxStreamedItems;
                AllowedContentTypes = allowedContentTypes;
                OwnerQueue = ownerQueue;
                Properties = properties;
                BodyProperty = bodyProperty;
                ETagProperty = etagProperty;
                ResponseETagProperty = responseETagProperty;
                IsRecord = isRecord;
                ConstructorParameters = constructorParameters;
                ServerSetProperties = properties
                    .Where(property => property.IsServerSet && property.HasPublicSetter)
                    .Select(property => property.Name)
                    .ToImmutableArray();
                InvalidServerSetProperties = invalidServerSetProperties;
                SuspiciousProperties = suspiciousProperties;
                AttachmentCount = attachmentCount;
                UnsupportedAttachmentCollections = unsupportedAttachmentCollections;
                AttachmentResponse = attachmentResponse;
                StreamElement = streamElement;
                Location = location;
                Diagnostics = diagnostics;
                IsValid = diagnostics.Count == 0;
            }

            private EndpointModel(string typeFullName, string typeName, IReadOnlyList<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                ApiGroup = "Ark";
                Summary = null;
                Remarks = null;
                Diagnostics = diagnostics;
                IsValid = false;
                Verb = string.Empty;
                Template = string.Empty;
                IsVersioned = false;
                Response = string.Empty;
                Kind = HandlerKind.None;
                AllowedContentTypes = ImmutableArray<string>.Empty;
                MaxFileCount = 0;
                Properties = ImmutableArray<PropertyModel>.Empty;
                BodyProperty = null;
                ETagProperty = null;
                ResponseETagProperty = null;
                ConstructorParameters = ImmutableArray<string>.Empty;
                ServerSetProperties = ImmutableArray<string>.Empty;
                InvalidServerSetProperties = ImmutableArray<string>.Empty;
                SuspiciousProperties = ImmutableArray<string>.Empty;
                UnsupportedAttachmentCollections = ImmutableArray<string>.Empty;
                AttachmentResponse = false;
                StreamElement = null;
            }

            public static EndpointModel Invalid(INamedTypeSymbol type, IReadOnlyList<DiagnosticInfo> diagnostics)
                => new(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.Name, diagnostics);

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string? Summary { get; }
            public string? Remarks { get; }
            public string ApiGroup { get; }
            public string Verb { get; }
            public string Template { get; }
            public bool IsVersioned { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int HttpIntroducedIn { get; }
            public int HttpRetiredIn { get; }
            public int SuccessStatusCode { get; }
            public int NullResultStatusCode { get; }
            public bool AcceptsMessagePack { get; }
            public bool AllowAnonymous { get; }

            public bool RequireAntiforgery { get; }

            public long MaxRequestBodySizeBytes { get; }
            public int MaxFileCount { get; }
            public int MaxMessagePackStreamedItems { get; }

            public ImmutableArray<string> AllowedContentTypes { get; }
            public string? OwnerQueue { get; }
            public ImmutableArray<PropertyModel> Properties { get; }
            public string? BodyProperty { get; }
            public string? ETagProperty { get; }
            public string? ResponseETagProperty { get; }
            public bool IsRecord { get; }
            public ImmutableArray<string> ConstructorParameters { get; }
            public ImmutableArray<string> ServerSetProperties { get; }
            public ImmutableArray<string> InvalidServerSetProperties { get; }
            public ImmutableArray<string> SuspiciousProperties { get; }
            public int AttachmentCount { get; }
            public ImmutableArray<string> UnsupportedAttachmentCollections { get; }
            public bool AttachmentResponse { get; }
            public string? StreamElement { get; }
            public bool IsStreaming => StreamElement is not null;
            public Location? Location { get; }
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
            public bool IsValid { get; }
        }

        private readonly record struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location, params object[] arguments)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = arguments.Length == 0 ? new object[] { typeName } : new[] { (object)typeName }.Concat(arguments).ToArray();
            }

            public DiagnosticDescriptor Descriptor { get; }
            public Location Location { get; }
            public object[] Arguments { get; }
        }

        private static Location GetLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        private readonly record struct PropertyModel
        {
            public PropertyModel(
                string name,
                string typeFullName,
                string? description,
                bool isString,
                bool isRoute,
                string bindingName,
                bool isQuery,
                bool isServerSet,
                bool isETag,
                bool isNullable,
                bool hasPublicSetter,
                bool isStringCollection,
                bool requiresTypeConverterBinding,
                bool isAttachmentCollection,
                bool isAttachmentArray,
                bool isBody)
            {
                Name = name;
                TypeFullName = typeFullName;
                Description = description;
                IsString = isString;
                IsRoute = isRoute;
                BindingName = bindingName;
                IsQuery = isQuery;
                IsServerSet = isServerSet;
                IsETag = isETag;
                IsNullable = isNullable;
                HasPublicSetter = hasPublicSetter;
                IsStringCollection = isStringCollection;
                RequiresTypeConverterBinding = requiresTypeConverterBinding;
                IsAttachmentCollection = isAttachmentCollection;
                IsAttachmentArray = isAttachmentArray;
                IsBody = isBody;
            }

            public string Name { get; }
            public string TypeFullName { get; }
            public string? Description { get; }
            public bool IsString { get; }
            public bool IsRoute { get; }
            public string BindingName { get; }
            public bool IsQuery { get; }
            public bool IsServerSet { get; }
            public bool IsETag { get; }
            public bool IsNullable { get; }
            public bool HasPublicSetter { get; }
            public bool IsStringCollection { get; }
            public bool RequiresTypeConverterBinding { get; }
            public bool IsAttachmentCollection { get; }
            public bool IsAttachmentArray { get; }
            public bool IsBody { get; }
        }
    }
}
