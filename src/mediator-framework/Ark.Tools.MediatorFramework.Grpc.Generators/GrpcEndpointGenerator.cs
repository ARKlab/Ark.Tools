// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
    /// <c>[GrpcMethod]</c> and emits code-first gRPC service contracts plus <c>MapArkGrpcServicesFromAssembly</c>
    /// inside a <c>partial ArkGeneratedEndpoints</c> class. Only the gRPC transport is emitted by
    /// this generator; add <c>Ark.Tools.MediatorFramework.MinimalApi.Generators</c> for HTTP and
    /// <c>Ark.Tools.MediatorFramework.Rebus.Generators</c> for Rebus.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkGrpcEndpointGenerator : IIncrementalGenerator
    {
        private const string GrpcMethodAttribute = "Ark.Tools.MediatorFramework.GrpcMethodAttribute";
        private const string GrpcServiceAttribute = "Ark.Tools.MediatorFramework.GrpcServiceAttribute";
        private const string ApiGroupAttribute = "Ark.Tools.MediatorFramework.ApiGroupAttribute";
        private const string VersioningAttribute = "Ark.Tools.MediatorFramework.VersioningAttribute";
        private const string ServerSetAttribute = "Ark.Tools.MediatorFramework.ServerSetAttribute";
        private const string ArkAttachment = "Ark.Tools.MediatorFramework.IArkAttachment";
        private const string ArkGenerateGrpcForAssemblyAttribute = "Ark.Tools.MediatorFramework.Grpc.ArkGenerateGrpcForAssemblyAttribute";
        private const string MappingParserTrackingName = "GrpcMappingParser";
        private const string EndpointParserTrackingName = "GrpcEndpointParser";
        private const string ModelTrackingName = "GrpcModel";
        private const string OutputTrackingName = "GrpcOutput";
        private const string AsyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";
        private static readonly string[] _collectionPrefixes =
        [
            "global::System.Collections.Generic.IEnumerable<",
            "global::System.Collections.Generic.IReadOnlyCollection<",
            "global::System.Collections.Generic.IReadOnlyList<",
            "global::System.Collections.Generic.ICollection<",
            "global::System.Collections.Generic.IList<",
            "global::System.Collections.Generic.List<",
            "global::System.Collections.Immutable.ImmutableArray<",
        ];

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointMappings = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => IsAssemblyMappingCandidate(node),
                    static (syntaxContext, cancellationToken) =>
                        GetAssemblyMapping(syntaxContext, cancellationToken))
                .WithTrackingName(MappingParserTrackingName)
                .Where(static mapping => mapping is not null)
                .Select(static (mapping, _) => mapping!.Value);
            var endpointAssemblies = endpointMappings
                .SelectMany(static (mapping, _) => mapping.AssemblyNames.Items)
                .Collect()
                .Select(static (assemblies, _) => new ImmutableEquatableArray<string>(assemblies));
            var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                    GrpcMethodAttribute,
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (attributeContext, _) => ExtractSourceEndpoint(attributeContext))
                .WithTrackingName(EndpointParserTrackingName)
                .Where(static endpoint => endpoint is not null)
                .Select(static (endpoint, _) => endpoint!.Value)
                .WithTrackingName(ModelTrackingName);
            var referencedEndpoints = context.CompilationProvider
                .Combine(endpointAssemblies)
                .SelectMany(static (pair, cancellationToken) =>
                    GetReferencedEndpoints(pair.Left, pair.Right.Items, cancellationToken));
            var compilationModel = context.CompilationProvider
                .Select(static (compilation, cancellationToken) =>
                    GetCompilationModel(compilation, cancellationToken));
            var collectedMappings = endpointMappings.Collect()
                .Select(static (mappings, _) => new ImmutableEquatableArray<AssemblyMapping>(mappings));

            var output = sourceEndpoints.Collect()
                .Select(static (endpoints, _) => new ImmutableEquatableArray<EndpointModel>(endpoints))
                .Combine(referencedEndpoints.Collect()
                    .Select(static (endpoints, _) => new ImmutableEquatableArray<EndpointModel>(endpoints)))
                .Combine(collectedMappings)
                .Combine(compilationModel)
                .Select(static (pair, cancellationToken) => BuildOutput(
                    pair.Left.Left.Left.Items.AddRange(pair.Left.Left.Right.Items),
                    pair.Right,
                    cancellationToken))
                .WithTrackingName(OutputTrackingName);

            context.RegisterSourceOutput(
                output,
                static (spc, output) => Emit(spc, output));
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol type)
                return null;

            var grpc = context.Attributes[0];
            var grpcServiceAttribute = context.SemanticModel.Compilation.GetTypeByMetadataName(GrpcServiceAttribute);
            var apiGroupAttribute = context.SemanticModel.Compilation.GetTypeByMetadataName(ApiGroupAttribute);
            var attachmentType = context.SemanticModel.Compilation.GetTypeByMetadataName(ArkAttachment);
            var grpcService = grpcServiceAttribute is null
                ? null
                : type.GetAttributes().FirstOrDefault(
                    attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, grpcServiceAttribute));
            return Extract(type, grpc, grpcService, apiGroupAttribute, attachmentType,
                context.SemanticModel.Compilation.GetTypeByMetadataName(AsyncEnumerable));
        }

        private static AssemblyMapping? GetAssemblyMapping(
            GeneratorSyntaxContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = (InvocationExpressionSyntax)context.Node;
            var genericName = GetInvokedGenericName(invocation);
            if (genericName is null)
                return null;

            var method = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var methodName = genericName.Identifier.ValueText;
            if ((method is null || !string.Equals(method.MetadataName, methodName, StringComparison.Ordinal))
                && !IsGeneratedEndpointInvocation(invocation, methodName, context.SemanticModel, cancellationToken))
                return null;

            var assemblyNames = methodName == "MapArkGrpcServices"
                ? GetContextAssemblyNames(context, genericName, cancellationToken)
                : GetAssemblyMarkerName(context, genericName, cancellationToken);
            return assemblyNames.IsDefaultOrEmpty ? null : new AssemblyMapping(assemblyNames);
        }

        private static bool IsAssemblyMappingCandidate(SyntaxNode node)
        {
            return node is InvocationExpressionSyntax invocation
                && GetInvokedGenericName(invocation) is not null;
        }

        private static GenericNameSyntax? GetInvokedGenericName(InvocationExpressionSyntax invocation)
        {
            var genericName = invocation.Expression switch
            {
                GenericNameSyntax directName => directName,
                MemberAccessExpressionSyntax { Name: GenericNameSyntax memberName } => memberName,
                MemberBindingExpressionSyntax { Name: GenericNameSyntax bindingName } => bindingName,
                _ => null,
            };
            return genericName is not null
                && genericName.TypeArgumentList.Arguments.Count == 1
                && genericName.Identifier.ValueText is "MapArkGrpcServicesFromAssembly" or "MapArkGrpcServices"
                    ? genericName
                    : null;
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
                .Where(attribute => attribute.AttributeClass?.ToDisplayString() == ArkGenerateGrpcForAssemblyAttribute)
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
            var grpcAttr = compilation.GetTypeByMetadataName(GrpcMethodAttribute);
            var grpcServiceAttr = compilation.GetTypeByMetadataName(GrpcServiceAttribute);
            var apiGroupAttr = compilation.GetTypeByMetadataName(ApiGroupAttribute);
            var attachmentType = compilation.GetTypeByMetadataName(ArkAttachment);
            var asyncEnumerableType = compilation.GetTypeByMetadataName(AsyncEnumerable);
            if (grpcAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = grpcAttr.ContainingAssembly;
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
                    var grpc = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, grpcAttr));
                    if (grpc is null)
                        continue;

                    var grpcService = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, grpcServiceAttr));
                    var model = Extract(type, grpc, grpcService, apiGroupAttr, attachmentType, asyncEnumerableType);
                    if (model is not null)
                        builder.Add(model.Value);
                }
            }

            return builder.ToImmutable();
        }

        private static IEnumerable<IAssemblySymbol> _relevantAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            yield return compilation.Assembly;

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

        private static IEnumerable<IAssemblySymbol> _referencedAssemblies(Compilation compilation, IAssemblySymbol runtimeAssembly)
        {
            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly).Skip(1))
                yield return assembly;
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
            AttributeData grpc,
            AttributeData? grpcService,
            INamedTypeSymbol? apiGroupAttribute,
            INamedTypeSymbol? attachmentType,
            INamedTypeSymbol? asyncEnumerableType)
        {
            string? response = null;
            var kind = HandlerKind.None;
            var attachmentResponse = false;
            string? streamElement = null;

            foreach (var iface in type.AllInterfaces)
            {
                var def = iface.OriginalDefinition;
                if (IsType(def, "IRequest`1", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Request;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (IsType(def, "IQuery`1", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Query;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (IsType(def, "ICommand", "Ark.Tools.Solid"))
                {
                    kind = HandlerKind.Command;
                    response = "global::Google.Protobuf.WellKnownTypes.Empty";
                    break;
                }
            }

            if (kind == HandlerKind.None || response is null)
                return EndpointModel.Invalid(type, new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedHandlerKind,
                    type.Name,
                    GetLocation(grpc)));

            var attachmentProperties = AllProperties(type)
.Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && (IsAttachmentType(property.Type, attachmentType) || IsAttachmentCollection(property.Type, attachmentType))).ToArray();
            var attachmentRequest = kind == HandlerKind.Command || attachmentProperties.Length == 0
                ? AttachmentRequestKind.None
                : IsAttachmentCollection(attachmentProperties[0].Type, attachmentType)
                    ? AttachmentRequestKind.Collection
                    : AttachmentRequestKind.Single;
            var grpcMethod = grpc.ConstructorArguments.FirstOrDefault().Value as string ?? type.Name;
            var grpcIntroducedIn = Version(type, "Introduced", 1);
            var grpcRetiredIn = Version(type, "Retired", 0);
            var apiGroup = apiGroupAttribute is null
                ? null
                : type.GetAttributes()
                    .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, apiGroupAttribute))
                    .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var defaultGroup = type.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString().Split('.').Last()
                : "Ark";
            var group = grpcService?.ConstructorArguments.FirstOrDefault().Value as string
                ?? apiGroup
                ?? defaultGroup;

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GeneratedName(type),
                grpcMethod,
                group,
                response,
                XmlDocumentation.Summary(type),
                XmlDocumentation.Remarks(type),
                kind,
                grpcIntroducedIn,
                grpcRetiredIn,
                attachmentResponse,
                streamElement,
                attachmentRequest,
                attachmentProperties.FirstOrDefault()?.Name,
                ImmutableEquatableArray<DiagnosticInfo>.Empty);
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

        private static bool IsAttachmentType(ITypeSymbol type, INamedTypeSymbol? attachmentType)
            => attachmentType is not null
                && (SymbolEqualityComparer.Default.Equals(type, attachmentType)
                    || type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, attachmentType)));

        private static bool IsType(INamedTypeSymbol type, string metadataName, string namespaceName)
            => string.Equals(type.MetadataName, metadataName, StringComparison.Ordinal)
                && string.Equals(type.ContainingNamespace.ToDisplayString(), namespaceName, StringComparison.Ordinal);

        private static bool IsAttribute(AttributeData attribute, string metadataName)
            => attribute.AttributeClass is not null
                && string.Equals(attribute.AttributeClass.MetadataName, metadataName.Substring(metadataName.LastIndexOf('.') + 1), StringComparison.Ordinal)
                && string.Equals(
                    attribute.AttributeClass.ContainingNamespace.ToDisplayString(),
                    metadataName[..metadataName.LastIndexOf('.')], StringComparison.Ordinal);

        private static bool IsAttachmentCollection(ITypeSymbol type, INamedTypeSymbol? attachmentType)
        {
            if (type is IArrayTypeSymbol array)
                return IsAttachmentType(array.ElementType, attachmentType);
            if (type is not INamedTypeSymbol named || !named.IsGenericType
                || !IsAttachmentType(named.TypeArguments[0], attachmentType))
                return false;
            return named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.IEnumerable<T>" or
                "System.Collections.Generic.List<T>" or
                "System.Collections.Generic.IReadOnlyList<T>" or
                "System.Collections.Generic.IReadOnlyCollection<T>";
        }

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

        private static Location GetLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        private static CompilationModel GetCompilationModel(
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            return new CompilationModel(
                GetProtoNamespace(compilation),
                new ImmutableEquatableArray<ProtoContractModel>(
                    GetProtoContracts(compilation, cancellationToken)));
        }

        private static GrpcOutput BuildOutput(
            ImmutableArray<EndpointModel> items,
            CompilationModel compilation,
            CancellationToken cancellationToken)
        {
            if (items.IsDefaultOrEmpty)
                return new GrpcOutput(null, ImmutableEquatableArray<DiagnosticInfo>.Empty);

            items = items.OrderBy(static item => item.TypeFullName, StringComparer.Ordinal).ToImmutableArray();
            var diagnostics = new ImmutableEquatableArray<DiagnosticInfo>(
                items.SelectMany(static item => item.Diagnostics.Items).ToImmutableArray());
            items = items.Where(static item => item.IsValid).ToImmutableArray();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.Tools.MediatorFramework.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Source-generated code-first gRPC transport hosting for pure Ark.Tools.Solid handlers.</summary>");
            sb.AppendLine("    public static partial class ArkGeneratedEndpoints");
            sb.AppendLine("    {");

            var maxVersion = items.IsDefaultOrEmpty
                ? 1
                : items.Max(static x => Math.Max(x.GrpcIntroducedIn, x.GrpcRetiredIn > 0 ? x.GrpcRetiredIn - 1 : 1));

            // Code-first gRPC service contracts (opt-in via [GrpcMethod]).
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup).OrderBy(static group => group.Key, StringComparer.Ordinal))
                {
                    for (var version = 1; version <= maxVersion; version++)
                    {
                        var active = group.Where(e => IsGrpcActive(e, version)).ToArray();
                        if (active.Length == 0)
                            continue;

                        var identifier = Identifier(group.Key) + "V" + version;
                        sb.AppendLine("        /// <summary>Generated code-first gRPC service contract for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                        sb.AppendLine("        [global::System.ServiceModel.ServiceContract(Name = " + Literal(group.Key + "V" + version) + ")]");
                        sb.AppendLine("        public interface I" + identifier + "GrpcService");
                        sb.AppendLine("        {");
                        foreach (var e in active)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (e.Summary is not null)
                                sb.AppendLine("            /// <summary>" + Escape(e.Summary) + "</summary>");
                            else
                                sb.AppendLine("            /// <summary>Dispatches " + e.TypeName + " to its pure handler.</summary>");
                            sb.AppendLine("            [global::System.ServiceModel.OperationContract(Name = " + Literal(e.GrpcMethod) + ")]");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                                sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(global::System.Collections.Generic.IAsyncEnumerable<global::Ark.Tools.MediatorFramework.UploadDocumentChunk> chunks, global::ProtoBuf.Grpc.CallContext context = default);");
                            else if (e.AttachmentResponse)
                                sb.AppendLine("            global::System.Collections.Generic.IAsyncEnumerable<global::Ark.Tools.MediatorFramework.DownloadDocumentChunk> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                            else if (e.IsStreaming)
                                sb.AppendLine("            global::System.Collections.Generic.IAsyncEnumerable<" + e.StreamElement + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                            else
                                sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                        }

                        sb.AppendLine("        }");
                        sb.AppendLine();
                        sb.AppendLine("        /// <summary>Generated partial gRPC implementation for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                        sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.Tools.MediatorFramework.Grpc.Generators\", \"1.0.0\")]");
                        sb.AppendLine("        public sealed partial class " + identifier + "GrpcService : I" + identifier + "GrpcService");
                        sb.AppendLine("        {");
                        sb.AppendLine("            private readonly global::System.IServiceProvider _services;");
                        sb.AppendLine("            /// <summary>Initializes a new instance.</summary>");
                        sb.AppendLine("            public " + identifier + "GrpcService(global::System.IServiceProvider services) { _services = services; }");
                        foreach (var e in active)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var processorService = e.Kind == HandlerKind.Query
                                ? "global::Ark.Tools.Solid.IQueryProcessor"
                                : e.Kind == HandlerKind.Command
                                    ? "global::Ark.Tools.Solid.ICommandProcessor"
                                    : "global::Ark.Tools.Solid.IRequestProcessor";
                            sb.AppendLine("            /// <inheritdoc />");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                            {
                                sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(global::System.Collections.Generic.IAsyncEnumerable<global::Ark.Tools.MediatorFramework.UploadDocumentChunk> chunks, global::ProtoBuf.Grpc.CallContext context = default)");
                            }
                            else if (e.AttachmentResponse)
                                sb.AppendLine("            public async global::System.Collections.Generic.IAsyncEnumerable<global::Ark.Tools.MediatorFramework.DownloadDocumentChunk> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            else if (e.IsStreaming)
                                sb.AppendLine("            public async global::System.Collections.Generic.IAsyncEnumerable<" + e.StreamElement + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            else
                                sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            sb.AppendLine("            {");
                            sb.AppendLine("                var processor = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<" + processorService + ">(_services);");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                            {
                                var attachmentValue = e.AttachmentRequest == AttachmentRequestKind.Collection
                                    ? "await global::Ark.Tools.MediatorFramework.StreamingArkAttachments.ReadAllAsync(chunks, context.CancellationToken).ConfigureAwait(false)"
                                    : "new global::Ark.Tools.MediatorFramework.StreamingArkAttachment(chunks)";
                                sb.AppendLine("                var request = new " + e.TypeFullName + " { " + e.AttachmentPropertyName + " = " + attachmentValue + " };");
                                sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, context.CancellationToken).ConfigureAwait(false);");
                                AppendNotFoundGuard(sb);
                                sb.AppendLine("                return result;");
                            }
                            else if (e.AttachmentResponse)
                            {
                                sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                if (result is null)");
                                sb.AppendLine("                    yield break;");
                                sb.AppendLine("                yield return new global::Ark.Tools.MediatorFramework.DownloadDocumentChunk { Metadata = new global::Ark.Tools.MediatorFramework.DownloadDocumentMetadata { Name = global::Ark.Tools.MediatorFramework.ArkAttachmentName.Sanitize(result.Name), ContentType = result.ContentType } };");
                                sb.AppendLine("                var stream = result.OpenRead();");
                                sb.AppendLine("                await using (stream.ConfigureAwait(false))");
                                sb.AppendLine("                {");
                                sb.AppendLine("                var buffer = new byte[64 * 1024];");
                                sb.AppendLine("                int bytesRead;");
                                sb.AppendLine("                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), context.CancellationToken).ConfigureAwait(false)) > 0)");
                                sb.AppendLine("                    yield return new global::Ark.Tools.MediatorFramework.DownloadDocumentChunk { Data = buffer[..bytesRead] };");
                                sb.AppendLine("                }");
                            }
                            else if (e.IsStreaming)
                            {
                                sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                await foreach (var item in result.WithCancellation(context.CancellationToken).ConfigureAwait(false))");
                                sb.AppendLine("                    yield return item;");
                            }
                            else if (e.Kind == HandlerKind.Command)
                            {
                                sb.AppendLine("                await processor.ExecuteAsync<" + e.TypeFullName + ">(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                return new global::Google.Protobuf.WellKnownTypes.Empty();");
                            }
                            else
                            {
                                sb.AppendLine("                var result = await processor.ExecuteAsync<" + e.TypeFullName + ", " + e.Response + ">(request, context.CancellationToken).ConfigureAwait(false);");
                                AppendNotFoundGuard(sb);
                                sb.AppendLine("                return result;");
                            }
                            sb.AppendLine("            }");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }
                }
            }

            // MapArkGrpcServicesFromAssembly is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every generated code-first gRPC service. TAssemblyMarker selects the assembly scanned for attributed contracts.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkGrpcServicesFromAssembly<TAssemblyMarker>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            sb.AppendLine("            var missingHandlers = new global::System.Collections.Generic.List<string>();");
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine("            VerifyGrpcHandlerRegistration(app.ServiceProvider, typeof(" + HandlerService(item) + "), " + Literal(item.TypeFullName) + ", missingHandlers);");
            }
            sb.AppendLine("            if (missingHandlers.Count > 0)");
            sb.AppendLine("                throw new global::System.InvalidOperationException(\"Missing mediator handler registrations: \" + string.Join(\"; \", missingHandlers));");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup).OrderBy(static group => group.Key, StringComparer.Ordinal))
                    for (var version = 1; version <= maxVersion; version++)
                        if (group.Any(e => IsGrpcActive(e, version)))
                            sb.AppendLine("            global::Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions.MapGrpcService<" + Identifier(group.Key) + "V" + version + "GrpcService>(app);");
            }
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Maps services selected by ArkGenerateGrpcForAssemblyAttribute on TContext.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkGrpcServices<TContext>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            sb.AppendLine("            return MapArkGrpcServicesFromAssembly<TContext>(app);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void VerifyGrpcHandlerRegistration(global::System.IServiceProvider services, global::System.Type handlerType, string contract, global::System.Collections.Generic.List<string> missingHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            var handlerVerifier = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<global::Ark.Tools.Solid.IMediatorHandlerRegistrationVerifier>(services);");
            sb.AppendLine("            if (services.GetService(handlerType) is null && (handlerVerifier is null || !handlerVerifier.IsRegistered(handlerType)))");
            sb.AppendLine("                missingHandlers.Add(contract + \" -> \" + handlerType);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            EmitProtoAssets(sb, items, compilation, cancellationToken);
            sb.AppendLine("}");

            return new GrpcOutput(sb.ToString(), diagnostics);
        }

        private static void Emit(SourceProductionContext context, GrpcOutput output)
        {
            foreach (var diagnostic in output.Diagnostics.Items)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    diagnostic.Descriptor,
                    diagnostic.Location.ToLocation(),
                    diagnostic.TypeName));
            }

            if (output.Source is not null)
                context.AddSource("ArkGeneratedEndpoints.Grpc.g.cs", SourceText.From(output.Source, Encoding.UTF8));
        }

        private static string HandlerService(EndpointModel item)
        {
            return item.Kind == HandlerKind.Query
                ? "global::Ark.Tools.Solid.IQueryHandler<" + item.TypeFullName + ", " + item.Response + ">"
                : item.Kind == HandlerKind.Command
                    ? "global::Ark.Tools.Solid.ICommandHandler<" + item.TypeFullName + ">"
                    : "global::Ark.Tools.Solid.IRequestHandler<" + item.TypeFullName + ", " + item.Response + ">";
        }

        private static string ProcessorService(EndpointModel item)
        {
            return item.Kind == HandlerKind.Query
                ? "global::Ark.Tools.Solid.IQueryProcessor"
                : item.Kind == HandlerKind.Command
                    ? "global::Ark.Tools.Solid.ICommandProcessor"
                    : "global::Ark.Tools.Solid.IRequestProcessor";
        }

        private static void AppendNotFoundGuard(StringBuilder sb)
        {
            sb.AppendLine("                if (result is null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var status = new global::Google.Rpc.Status");
            sb.AppendLine("                    {");
            sb.AppendLine("                        Code = (int)global::Grpc.Core.StatusCode.NotFound,");
            sb.AppendLine("                        Message = \"The requested resource was not found.\",");
            sb.AppendLine("                    };");
            sb.AppendLine("                    throw global::Grpc.Core.RpcStatusExtensions.ToRpcException(status);");
            sb.AppendLine("                }");
        }

        private static void EmitProtoAssets(
            StringBuilder sb,
            ImmutableArray<EndpointModel> items,
            CompilationModel compilation,
            CancellationToken cancellationToken)
        {
            sb.AppendLine("    /// <summary>Source-generated protobuf assets for the discovered gRPC contracts.</summary>");
            sb.AppendLine("    public static class ArkGeneratedProtos");
            sb.AppendLine("    {");
            var contracts = compilation.ProtoContracts.Items;
            var contractsByType = new Dictionary<string, ProtoContractModel>(StringComparer.Ordinal);
            var contractsByName = new Dictionary<string, ProtoContractModel>(StringComparer.Ordinal);
            foreach (var contract in contracts)
            {
                contractsByType.TryAdd(contract.TypeFullName, contract);
                contractsByName.TryAdd(contract.Name, contract);
            }
            var contractLookup = new ProtoContractLookup(contractsByType, contractsByName);
            var entries = new List<string>();
            var content = new StringBuilder();
            foreach (var group in items.GroupBy(static item => item.ServiceGroup).OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var active = group.ToArray();
                var requestNames = active
                    .Select(item => ProtoTypeName(item.TypeFullName, contractLookup))
                    .ToHashSet(StringComparer.Ordinal);
                var reachable = new HashSet<string>(StringComparer.Ordinal);
                foreach (var endpoint in active)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddReachable(endpoint.TypeFullName, contractLookup, reachable);
                    AddReachable(endpoint.IsStreaming ? endpoint.StreamElement! : endpoint.Response, contractLookup, reachable);
                }

                content.Clear();
                content.AppendLine("syntax = \"proto3\";");
                content.AppendLine();
                content.Append("option csharp_namespace = ")
                    .Append(Literal(compilation.ProtoNamespace))
                    .AppendLine(";");
                content.AppendLine();
                content.AppendLine("import \"google/type/date.proto\";");
                content.AppendLine("import \"google/type/datetime.proto\";");
                content.AppendLine("import \"google/protobuf/empty.proto\";");
                if (reachable.Any(type => contractLookup.ByType.TryGetValue(type, out var contract)
                    && contract.Members.Items.Any(member => IsArkNodaTimePeriod(member.Type))))
                {
                    content.AppendLine("import \"ark/nodatime.proto\";");
                }
                if (active.Any(item => item.AttachmentResponse || item.AttachmentRequest != AttachmentRequestKind.None))
                    content.AppendLine("import \"ark/mediator.proto\";");
                content.AppendLine();
                if (active.Any(item => item.AttachmentResponse))
                {
                    content.AppendLine("message DownloadDocumentMetadata {");
                    content.AppendLine("  string name = 1;");
                    content.AppendLine("  string content_type = 2;");
                    content.AppendLine("  optional int64 length = 3;");
                    content.AppendLine("}");
                    content.AppendLine();
                    content.AppendLine("message DownloadDocumentChunk {");
                    content.AppendLine("  oneof content {");
                    content.AppendLine("    DownloadDocumentMetadata metadata = 1;");
                    content.AppendLine("    bytes data = 2;");
                    content.AppendLine("  }");
                    content.AppendLine("}");
                    content.AppendLine();
                }
                foreach (var contract in contracts
                    .Where(contract => reachable.Contains(contract.TypeFullName))
                    .OrderBy(static contract => contract.Name, StringComparer.Ordinal))
                    EmitProtoMessage(content, contract, contractLookup, requestNames.Contains(contract.Name));

                var maxVersion = active.Max(static x => Math.Max(
                    x.GrpcIntroducedIn,
                    x.GrpcRetiredIn > 0 ? x.GrpcRetiredIn - 1 : 1));
                for (var version = 1; version <= maxVersion; version++)
                {
                    var versionItems = active.Where(item => IsGrpcActive(item, version))
                        .OrderBy(static item => item.TypeName, StringComparer.Ordinal)
                        .ToArray();
                    if (versionItems.Length == 0)
                        continue;

                    content.Append("service ").Append(Identifier(group.Key)).Append('V').Append(version).AppendLine(" {");
                    foreach (var item in versionItems)
                    {
                        WriteComment(content, item.Summary, "  ");
                        content.Append("  rpc ").Append(item.GrpcMethod)
                            .Append(item.AttachmentRequest != AttachmentRequestKind.None
                                ? "(stream ark.mediator.UploadDocumentChunk) returns "
                                : "(" + ProtoTypeName(item.TypeFullName, contractLookup) + ") returns ");
                        if (item.AttachmentResponse)
                            content.Append("(stream DownloadDocumentChunk);");
                        else if (item.IsStreaming)
                            content.Append("(stream ").Append(ProtoTypeName(item.StreamElement!, contractLookup)).Append(");");
                        else
                            content.Append('(').Append(ProtoTypeName(item.Response, contractLookup)).Append(");");
                        content.AppendLine();
                    }
                    content.AppendLine("}");
                    content.AppendLine();
                }

                var fileName = Identifier(group.Key) + ".proto";
                EmitProtoEntry(sb, fileName, content.ToString());
                entries.Add("Get" + Identifier(Path.GetFileNameWithoutExtension(fileName)) + "()");
            }

            sb.AppendLine("        public static (string FileName, string Content)[] GetFiles() => new[]");
            sb.AppendLine("        {");
            foreach (var entry in entries)
                sb.Append("            ").Append(entry).AppendLine(",");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
        }

        private static string GetProtoNamespace(Compilation compilation)
        {
            var assemblyName = compilation.AssemblyName ?? "Ark.Tools.MediatorFramework";
            return assemblyName.EndsWith(".WebInterface", StringComparison.Ordinal)
                ? assemblyName[..^".WebInterface".Length] + ".GrpcClient"
                : assemblyName + ".GrpcClient";
        }

        private static void EmitProtoEntry(StringBuilder sb, string fileName, string content)
        {
            sb.Append("        public static (string FileName, string Content) ")
                .Append("Get").Append(Identifier(Path.GetFileNameWithoutExtension(fileName)))
                .AppendLine("() => (")
                .Append("            ").Append(Literal(fileName)).AppendLine(",")
                .Append("            ").Append(Literal(content)).AppendLine(");");
        }

        private static void EmitProtoMessage(
            StringBuilder sb,
            ProtoContractModel contract,
            ProtoContractLookup contractLookup,
            bool isRequest)
        {
            WriteComment(sb, contract.Summary);
            sb.Append("message ").Append(contract.Name).AppendLine(" {");
            foreach (var include in contract.Includes.Items)
            {
                sb.Append("  ").Append(SimpleName(include.TypeName)).Append(' ')
                    .Append(SnakeCase(SimpleName(include.TypeName))).Append(" = ")
                    .Append(include.Number).AppendLine(";");
            }

            foreach (var member in contract.Members.Items
                .Where(member => !isRequest || (!member.IsServerSet
                    && !member.Type.Contains("IArkAttachment", StringComparison.Ordinal)))
                .OrderBy(static member => member.Number))
            {
                WriteComment(sb, member.Description);
                var type = member.PrecomputedProtoType ?? ProtoTypeName(member.Type, contractLookup);
                sb.Append("  ");
                if (member.IsRepeated)
                    sb.Append("repeated ");
                sb.Append(type).Append(' ').Append(SnakeCase(member.Name)).Append(" = ")
                    .Append(member.Number).AppendLine(";");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static ImmutableArray<ProtoContractModel> GetProtoContracts(
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            var protoAttribute = compilation.GetTypeByMetadataName("ProtoBuf.ProtoContractAttribute");
            if (protoAttribute is null)
                return ImmutableArray<ProtoContractModel>.Empty;

            var result = ImmutableArray.CreateBuilder<ProtoContractModel>();
            foreach (var assembly in _relevantAssemblies(compilation, protoAttribute.ContainingAssembly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var type in _allTypes(assembly.GlobalNamespace)
                    .Where(type => type.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, protoAttribute))))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var protoContract = type.GetAttributes().First(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, protoAttribute));
                    var members = AllProperties(type)
                        .Select(property => new
                        {
                            Property = property,
                            Attribute = property.GetAttributes().FirstOrDefault(attribute =>
                                attribute.AttributeClass?.ToDisplayString() == "ProtoBuf.ProtoMemberAttribute"),
                        })
                        .Where(item => item.Attribute is not null)
                        .Select(item => new ProtoMemberModel(
                            item.Property.Name,
                            item.Property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            XmlDocumentation.Summary(item.Property),
                            item.Attribute!.ConstructorArguments.FirstOrDefault().Value is int number ? number : 0,
                            IsRepeatedProtoType(item.Property.Type),
                        item.Property.GetAttributes().Any(attribute =>
                            attribute.AttributeClass?.ToDisplayString() == ServerSetAttribute),
                        item.Property.Type is INamedTypeSymbol evolvableEnum && IsEvolvableEnum(evolvableEnum)
                            ? EvolvableEnumProtoType(evolvableEnum)
                            : null))
                        .Where(member => member.Number > 0)
                        .ToImmutableArray();

                    var includes = type.GetAttributes()
                        .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "ProtoBuf.ProtoIncludeAttribute")
                        .Select(attribute => new
                        {
                            Type = attribute.ConstructorArguments.ElementAtOrDefault(1).Value as INamedTypeSymbol,
                            Number = attribute.ConstructorArguments.FirstOrDefault().Value is int number ? number : 0,
                        })
                        .Where(include => include.Type is not null && include.Number > 0)
                        .Select(include => new ProtoIncludeModel(
                            include.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            include.Number))
                        .ToImmutableArray();

                    var name = protoContract.NamedArguments
                        .FirstOrDefault(argument => argument.Key == "Name")
                        .Value.Value as string;
                    result.Add(new ProtoContractModel(
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        string.IsNullOrWhiteSpace(name) ? GeneratedName(type) : name!,
                        XmlDocumentation.Summary(type),
                        new ImmutableEquatableArray<ProtoMemberModel>(members),
                        new ImmutableEquatableArray<ProtoIncludeModel>(includes)));
                }
            }
            return result.ToImmutable();
        }

        private static void AddReachable(
            string displayName,
            ProtoContractLookup contractLookup,
            ISet<string> reachable)
        {
            var name = SimpleName(displayName);
            var contract = contractLookup.ByType.TryGetValue(displayName, out var byType)
                ? byType
                : contractLookup.ByName.TryGetValue(name, out var byName)
                    ? byName
                    : null;
            if (contract is null || !reachable.Add(contract.TypeFullName))
                return;

            foreach (var member in contract.Members.Items)
                AddReachable(member.Type, contractLookup, reachable);
            foreach (var include in contract.Includes.Items)
                AddReachable(include.TypeName, contractLookup, reachable);
        }

        private static string ProtoTypeName(string typeName, ProtoContractLookup contractLookup)
        {
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
                return ProtoTypeName(typeName[..^2], contractLookup);

            foreach (var collectionPrefix in _collectionPrefixes)
            {
                if (typeName.StartsWith(collectionPrefix, StringComparison.Ordinal)
                    && typeName.EndsWith(">", StringComparison.Ordinal))
                    return ProtoTypeName(typeName[collectionPrefix.Length..^1], contractLookup);
            }

            if (typeName.StartsWith("global::System.Nullable<", StringComparison.Ordinal)
                && typeName.EndsWith(">", StringComparison.Ordinal))
                return ProtoTypeName(typeName["global::System.Nullable<".Length..^1], contractLookup);

            if (typeName.StartsWith("global::Ark.Tools.Core.EvolvableEnum<", StringComparison.Ordinal))
            {
                var arguments = typeName["global::Ark.Tools.Core.EvolvableEnum<".Length..^1]
                    .Split(',');
                var backing = arguments.Length == 1 ? "int" : arguments[1].Trim();
                return backing switch
                {
                    "sbyte" or "short" or "int" or "global::System.SByte" or "global::System.Int16" or "global::System.Int32" => "int32",
                    "byte" or "ushort" or "uint" or "global::System.Byte" or "global::System.UInt16" or "global::System.UInt32" => "uint32",
                    "long" or "global::System.Int64" => "int64",
                    "ulong" or "global::System.UInt64" => "uint64",
                    _ => "int32",
                };
            }

            var name = typeName switch
            {
                "string" => "string",
                "bool" => "bool",
                "long" => "int64",
                "ulong" => "uint64",
                "int" or "short" or "sbyte" or "byte" => "int32",
                "uint" or "ushort" => "uint32",
                "float" => "float",
                "double" => "double",
                "global::System.String" => "string",
                "global::System.Boolean" => "bool",
                "global::System.Int64" => "int64",
                "global::System.UInt64" => "uint64",
                "global::System.SByte" => "int32",
                "global::System.Int32" or "global::System.Int16" or "global::System.Byte" => "int32",
                "global::System.UInt32" or "global::System.UInt16" => "uint32",
                "global::System.Single" => "float",
                "global::System.Double" => "double",
                "global::System.Guid" => "bytes",
                "global::NodaTime.LocalDate" => "google.type.Date",
                "global::NodaTime.LocalDateTime" => "google.type.DateTime",
                "global::NodaTime.OffsetDateTime" => "google.type.DateTime",
                "global::NodaTime.ZonedDateTime" => "google.type.DateTime",
                "global::NodaTime.Period" => "ark.nodatime.Period",
                "global::Google.Protobuf.WellKnownTypes.Empty" => "google.protobuf.Empty",
                _ => null,
            };
            if (name is not null)
                return name;

            return contractLookup.ByType.TryGetValue(typeName, out var contract)
                ? contract.Name
                : "bytes";
        }

        private static bool IsRepeatedProtoType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
                return true;
            if (type is not INamedTypeSymbol named || !named.IsGenericType)
                return false;
            return named.Name is "IEnumerable" or "IReadOnlyCollection" or "IReadOnlyList"
                or "ICollection" or "IList" or "List" or "ImmutableArray";
        }

        private static string SimpleName(string value)
        {
            var separator = value.LastIndexOf('.');
            return separator < 0 ? value : value[(separator + 1)..];
        }

        private static bool IsArkNodaTimePeriod(string typeName)
        {
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
                return IsArkNodaTimePeriod(typeName[..^2]);
            if (typeName.StartsWith("global::System.Nullable<", StringComparison.Ordinal)
                && typeName.EndsWith(">", StringComparison.Ordinal))
                return IsArkNodaTimePeriod(typeName["global::System.Nullable<".Length..^1]);
            return string.Equals(typeName, "global::NodaTime.Period", StringComparison.Ordinal);
        }

        // Detects Ark.Tools.Core.EvolvableEnum by name/arity/namespace (no compile-time
        // reference to Ark.Tools.Core is required, matching this generator's convention of
        // recognizing well-known types by their fully-qualified name/shape).
        private static bool IsEvolvableEnum(INamedTypeSymbol named) =>
            named.IsGenericType && named.Arity is 1 or 2
            && named.OriginalDefinition.Name == "EvolvableEnum"
            && named.ContainingNamespace?.ToDisplayString() == "Ark.Tools.Core";

        private static string EvolvableEnumProtoType(INamedTypeSymbol type)
        {
            var backing = type.Arity == 1 ? SpecialType.System_Int32 : type.TypeArguments[1].SpecialType;
            return ProtoIntegerType(backing)
                ?? ProtoIntegerType((type.TypeArguments[0] as INamedTypeSymbol)?.EnumUnderlyingType?.SpecialType)
                ?? "int32";
        }

        private static string? ProtoIntegerType(SpecialType? backing)
            => backing switch
            {
                SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_Int32 => "int32",
                SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_UInt32 => "uint32",
                SpecialType.System_Int64 => "int64",
                SpecialType.System_UInt64 => "uint64",
                _ => null,
            };

        private static IEnumerable<IPropertySymbol> AllProperties(INamedTypeSymbol type)
        {
            for (var current = type; current is not null; current = current.BaseType)
                foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                    yield return property;
        }

        private static string SnakeCase(string value)
        {
            var builder = new StringBuilder(value.Length + 4);
            foreach (var character in value)
            {
                if (char.IsUpper(character) && builder.Length > 0)
                    builder.Append('_');
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static bool IsGrpcActive(EndpointModel endpoint, int version)
        {
            return version >= endpoint.GrpcIntroducedIn
                && (endpoint.GrpcRetiredIn == 0 || version < endpoint.GrpcRetiredIn);
        }

        private static string Literal(string value)
            => SyntaxFactory.Literal(value).ToFullString();

        private static void WriteComment(StringBuilder builder, string? text, string indent = "")
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            var current = new StringBuilder();
            foreach (var line in text!.Split('\n'))
            {
                var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                current.Clear();
                foreach (var word in words)
                {
                    if (current.Length > 0 && current.Length + word.Length + 1 > 96)
                    {
                        builder.Append(indent).Append("// ").AppendLine(current.ToString());
                        current.Clear();
                    }
                    if (current.Length > 0)
                        current.Append(' ');
                    current.Append(word);
                }
                if (current.Length > 0)
                    builder.Append(indent).Append("// ").AppendLine(current.ToString());
            }
        }

        private static string Identifier(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var character in value)
                sb.Append(char.IsLetterOrDigit(character) ? character : '_');
            return sb.Length == 0 ? "Ark" : sb.ToString();
        }

        private static string Escape(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private enum HandlerKind
        {
            None = 0,
            Request = 1,
            Query = 2,
            Command = 3,
        }

        private enum AttachmentRequestKind
        {
            None = 0,
            Single = 1,
            Collection = 2,
        }

        private readonly record struct AssemblyMapping(ImmutableEquatableArray<string> AssemblyNames)
        {
            public AssemblyMapping(ImmutableArray<string> assemblyNames)
                : this(new ImmutableEquatableArray<string>(assemblyNames))
            {
            }
        }

        private readonly record struct EndpointModel
        {
            public EndpointModel(string typeFullName, string typeName, string grpcMethod, string serviceGroup, string response, string? summary, string? remarks, HandlerKind kind, int grpcIntroducedIn, int grpcRetiredIn, bool attachmentResponse, string? streamElement, AttachmentRequestKind attachmentRequest, string? attachmentPropertyName, ImmutableEquatableArray<DiagnosticInfo> diagnostics)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                GrpcMethod = grpcMethod;
                ServiceGroup = serviceGroup;
                Response = response;
                Summary = summary;
                Remarks = remarks;
                Kind = kind;
                GrpcIntroducedIn = grpcIntroducedIn;
                GrpcRetiredIn = grpcRetiredIn;
                AttachmentResponse = attachmentResponse;
                StreamElement = streamElement;
                AttachmentRequest = attachmentRequest;
                AttachmentPropertyName = attachmentPropertyName;
                Diagnostics = diagnostics;
                IsValid = diagnostics.Items.IsEmpty;
            }

            private EndpointModel(INamedTypeSymbol type, DiagnosticInfo diagnostic)
            {
                TypeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                TypeName = GeneratedName(type);
                Diagnostics = new ImmutableEquatableArray<DiagnosticInfo>(ImmutableArray.Create(diagnostic));
                IsValid = false;
                GrpcMethod = string.Empty;
                ServiceGroup = string.Empty;
                Response = string.Empty;
                Summary = null;
                Remarks = null;
                AttachmentResponse = false;
                StreamElement = null;
                AttachmentRequest = AttachmentRequestKind.None;
                AttachmentPropertyName = null;
            }

            public static EndpointModel Invalid(INamedTypeSymbol type, DiagnosticInfo diagnostic)
                => new(type, diagnostic);

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string GrpcMethod { get; }
            public string ServiceGroup { get; }
            public string Response { get; }
            public string? Summary { get; }
            public string? Remarks { get; }
            public HandlerKind Kind { get; }
            public int GrpcIntroducedIn { get; }
            public int GrpcRetiredIn { get; }
            public bool AttachmentResponse { get; }
            public string? StreamElement { get; }
            public AttachmentRequestKind AttachmentRequest { get; }
            public string? AttachmentPropertyName { get; }
            public bool IsStreaming => StreamElement is not null;
            public ImmutableEquatableArray<DiagnosticInfo> Diagnostics { get; }
            public bool IsValid { get; }
        }

        private readonly record struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location)
            {
                Descriptor = descriptor;
                TypeName = typeName;
                Location = LocationSpec.Create(location);
            }

            public DiagnosticDescriptor Descriptor { get; }
            public string TypeName { get; }
            public LocationSpec Location { get; }
        }

        private static string GeneratedName(INamedTypeSymbol type)
        {
            var names = new Stack<string>();
            for (var current = type; current is not null; current = current.ContainingType)
                names.Push(current.Name);
            return string.Join("_", names);
        }

        private sealed record ProtoContractLookup(
            IReadOnlyDictionary<string, ProtoContractModel> ByType,
            IReadOnlyDictionary<string, ProtoContractModel> ByName);

        private sealed record ProtoContractModel(
            string TypeFullName,
            string Name,
            string? Summary,
            ImmutableEquatableArray<ProtoMemberModel> Members,
            ImmutableEquatableArray<ProtoIncludeModel> Includes);

        private readonly record struct ProtoMemberModel(
            string Name,
            string Type,
            string? Description,
            int Number,
            bool IsRepeated,
            bool IsServerSet,
            string? PrecomputedProtoType);

        private readonly record struct ProtoIncludeModel(string TypeFullName, int Number)
        {
            public string TypeName => SimpleName(TypeFullName);
        }

        private sealed record CompilationModel(
            string ProtoNamespace,
            ImmutableEquatableArray<ProtoContractModel> ProtoContracts);

        private readonly record struct GrpcOutput(
            string? Source,
            ImmutableEquatableArray<DiagnosticInfo> Diagnostics);

        private readonly record struct LocationSpec(
            string FilePath,
            SyntaxTree? SourceTree,
            int Start,
            int Length,
            int StartLine,
            int StartCharacter,
            int EndLine,
            int EndCharacter)
        {
            public static LocationSpec Create(Location? location)
            {
                if (location is null || !location.IsInSource)
                    return default;

                var lineSpan = location.GetLineSpan();
                var filePath = location.SourceTree?.FilePath ?? string.Empty;
                return new LocationSpec(
                    filePath,
                    filePath.Length == 0 ? location.SourceTree : null,
                    location.SourceSpan.Start,
                    location.SourceSpan.Length,
                    lineSpan.StartLinePosition.Line,
                    lineSpan.StartLinePosition.Character,
                    lineSpan.EndLinePosition.Line,
                    lineSpan.EndLinePosition.Character);
            }

            public Location ToLocation()
            {
                if (SourceTree is not null)
                    return Location.Create(SourceTree, new TextSpan(Start, Length));

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
            public static ImmutableEquatableArray<T> Empty { get; } =
                new(ImmutableArray<T>.Empty);

            public ImmutableEquatableArray(ImmutableArray<T> items)
            {
                Items = items;
            }

            public ImmutableArray<T> Items { get; }

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
}
