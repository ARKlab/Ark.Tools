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

namespace Ark.MediatorFramework.Generators
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
        private const string GrpcMethodAttribute = "Ark.MediatorFramework.GrpcMethodAttribute";
        private const string GrpcServiceAttribute = "Ark.MediatorFramework.GrpcServiceAttribute";
        private const string ApiGroupAttribute = "Ark.MediatorFramework.ApiGroupAttribute";
        private const string VersioningAttribute = "Ark.MediatorFramework.VersioningAttribute";
        private const string ServerSetAttribute = "Ark.MediatorFramework.ServerSetAttribute";
        private const string ArkAttachment = "Ark.MediatorFramework.IArkAttachment";
        private const string AsyncEnumerable = "System.Collections.Generic.IAsyncEnumerable`1";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpointAssemblies = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is InvocationExpressionSyntax invocation
                        && IsInvocationNamed(invocation, "MapArkGrpcServicesFromAssembly"),
                    static (syntaxContext, cancellationToken) =>
                        GetAssemblyName(syntaxContext, "MapArkGrpcServicesFromAssembly", cancellationToken))
                .Where(static assemblyName => assemblyName is not null)
                .Select(static (assemblyName, _) => assemblyName!)
                .Collect();
            var sourceEndpoints = context.SyntaxProvider.ForAttributeWithMetadataName(
                    GrpcMethodAttribute,
                    static (_, _) => true,
                    static (attributeContext, _) => ExtractSourceEndpoint(attributeContext))
                .Where(static endpoint => endpoint is not null)
                .Select(static (endpoint, _) => endpoint!.Value);
            var referencedEndpoints = context.CompilationProvider
                .Combine(endpointAssemblies)
                .SelectMany(static (pair, cancellationToken) =>
                    GetReferencedEndpoints(pair.Left, pair.Right, cancellationToken));

            var collected = sourceEndpoints.Collect().Combine(referencedEndpoints.Collect());

            context.RegisterSourceOutput(
                collected.Combine(context.CompilationProvider),
                static (spc, pair) => Emit(spc, pair.Left.Left.AddRange(pair.Left.Right), pair.Right));
        }

        private static EndpointModel? ExtractSourceEndpoint(GeneratorAttributeSyntaxContext context)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
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

        private static bool IsInvocationNamed(InvocationExpressionSyntax invocation, string methodName)
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess =>
                    memberAccess.Name.Identifier.ValueText == methodName,
                GenericNameSyntax genericName =>
                    genericName.Identifier.ValueText == methodName,
                IdentifierNameSyntax identifierName =>
                    identifierName.Identifier.ValueText == methodName,
                _ => false,
            };
        }

        private static string? GetAssemblyName(
            GeneratorSyntaxContext context,
            string methodName,
            CancellationToken cancellationToken)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var genericName = invocation.Expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .FirstOrDefault(name => name.Identifier.ValueText == methodName);
            if (genericName is null || genericName.TypeArgumentList.Arguments.Count != 1)
                return null;

            return context.SemanticModel
                .GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken)
                .Type?.ContainingAssembly?.Name;
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

            foreach (var assembly in _referencedAssemblies(compilation, runtimeAssembly)
                .Where(assembly => endpointAssemblies.Contains(assembly.Name, StringComparer.Ordinal)))
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
                var def = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def == "global::Ark.Tools.Solid.IRequest<TResponse>")
                {
                    kind = HandlerKind.Request;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (def == "global::Ark.Tools.Solid.IQuery<TResult>")
                {
                    kind = HandlerKind.Query;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    attachmentResponse = IsAttachmentType(iface.TypeArguments[0], attachmentType);
                    streamElement = GetAsyncEnumerableElement(iface.TypeArguments[0], asyncEnumerableType);
                    break;
                }

                if (def == "global::Ark.Tools.Solid.ICommand")
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
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                .Where(property => IsAttachmentType(property.Type, attachmentType) || IsAttachmentCollection(property.Type, attachmentType))
                .ToArray();
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
                Array.Empty<DiagnosticInfo>(),
                type.Locations.FirstOrDefault());
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static int Version(INamedTypeSymbol type, string propertyName, int defaultValue)
        {
            var attribute = type.GetAttributes().FirstOrDefault(
                candidate => candidate.AttributeClass?.ToDisplayString() == VersioningAttribute);
            return attribute is null ? defaultValue : NamedInt(attribute, propertyName, defaultValue);
        }

        private static bool IsAttachmentType(ITypeSymbol type, INamedTypeSymbol? attachmentType)
            => attachmentType is not null
                && (SymbolEqualityComparer.Default.Equals(type, attachmentType)
                    || type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, attachmentType)));

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

        private static void Emit(
            SourceProductionContext spc,
            ImmutableArray<EndpointModel> items,
            Compilation compilation)
        {
            if (items.IsDefaultOrEmpty)
                return;

            foreach (var item in items)
                foreach (var diagnostic in item.Diagnostics)
                    spc.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments));
            items = items.Where(static item => item.IsValid).ToImmutableArray();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Ark.MediatorFramework.Generated");
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
                foreach (var group in items.GroupBy(static x => x.ServiceGroup))
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
                            if (e.Summary is not null)
                                sb.AppendLine("            /// <summary>" + Escape(e.Summary) + "</summary>");
                            else
                                sb.AppendLine("            /// <summary>Dispatches " + e.TypeName + " to its pure handler.</summary>");
                            sb.AppendLine("            [global::System.ServiceModel.OperationContract(Name = " + Literal(e.GrpcMethod) + ")]");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                                sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(global::System.Collections.Generic.IAsyncEnumerable<global::Ark.MediatorFramework.UploadDocumentChunk> chunks, global::ProtoBuf.Grpc.CallContext context = default);");
                            else if (e.AttachmentResponse)
                                sb.AppendLine("            global::System.Collections.Generic.IAsyncEnumerable<global::Ark.MediatorFramework.DownloadDocumentChunk> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                            else if (e.IsStreaming)
                                sb.AppendLine("            global::System.Collections.Generic.IAsyncEnumerable<" + e.StreamElement + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                            else
                                sb.AppendLine("            global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default);");
                        }

                        sb.AppendLine("        }");
                        sb.AppendLine();
                        sb.AppendLine("        /// <summary>Generated partial gRPC implementation for the " + Escape(group.Key) + " v" + version + " group.</summary>");
                        sb.AppendLine("        [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.MediatorFramework.Grpc.Generators\", \"1.0.0\")]");
                        sb.AppendLine("        public sealed partial class " + identifier + "GrpcService : I" + identifier + "GrpcService");
                        sb.AppendLine("        {");
                        sb.AppendLine("            private readonly global::SimpleInjector.Container _container;");
                        sb.AppendLine("            /// <summary>Initializes a new instance.</summary>");
                        sb.AppendLine("            public " + identifier + "GrpcService(global::SimpleInjector.Container container) { _container = container; }");
                        foreach (var e in active)
                        {
                            var handlerService = e.Kind == HandlerKind.Query
                                ? "global::Ark.Tools.Solid.IQueryHandler<" + e.TypeFullName + ", " + e.Response + ">"
                                : e.Kind == HandlerKind.Command
                                    ? "global::Ark.Tools.Solid.ICommandHandler<" + e.TypeFullName + ">"
                                    : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                            sb.AppendLine("            /// <inheritdoc />");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                            {
                                sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(global::System.Collections.Generic.IAsyncEnumerable<global::Ark.MediatorFramework.UploadDocumentChunk> chunks, global::ProtoBuf.Grpc.CallContext context = default)");
                            }
                            else if (e.AttachmentResponse)
                                sb.AppendLine("            public async global::System.Collections.Generic.IAsyncEnumerable<global::Ark.MediatorFramework.DownloadDocumentChunk> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            else if (e.IsStreaming)
                                sb.AppendLine("            public async global::System.Collections.Generic.IAsyncEnumerable<" + e.StreamElement + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            else
                                sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            sb.AppendLine("            {");
                            sb.AppendLine("                var handler = _container.GetInstance<" + handlerService + ">();");
                            if (e.AttachmentRequest != AttachmentRequestKind.None)
                            {
                                var attachmentValue = e.AttachmentRequest == AttachmentRequestKind.Collection
                                    ? "await global::Ark.MediatorFramework.StreamingArkAttachments.ReadAllAsync(chunks, context.CancellationToken).ConfigureAwait(false)"
                                    : "new global::Ark.MediatorFramework.StreamingArkAttachment(chunks)";
                                sb.AppendLine("                var request = new " + e.TypeFullName + " { " + e.AttachmentPropertyName + " = " + attachmentValue + " };");
                                sb.AppendLine("                var result = await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                                AppendNotFoundGuard(sb);
                                sb.AppendLine("                return result;");
                            }
                            else if (e.AttachmentResponse)
                            {
                                sb.AppendLine("                var result = await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                if (result is null)");
                                sb.AppendLine("                    yield break;");
                                sb.AppendLine("                yield return new global::Ark.MediatorFramework.DownloadDocumentChunk { Metadata = new global::Ark.MediatorFramework.DownloadDocumentMetadata { Name = global::Ark.MediatorFramework.ArkAttachmentName.Sanitize(result.Name), ContentType = result.ContentType } };");
                                sb.AppendLine("                await using var stream = result.OpenRead();");
                                sb.AppendLine("                var buffer = new byte[64 * 1024];");
                                sb.AppendLine("                int bytesRead;");
                                sb.AppendLine("                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), context.CancellationToken).ConfigureAwait(false)) > 0)");
                                sb.AppendLine("                    yield return new global::Ark.MediatorFramework.DownloadDocumentChunk { Data = buffer[..bytesRead] };");
                            }
                            else if (e.IsStreaming)
                            {
                                sb.AppendLine("                var result = await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                await foreach (var item in result.WithCancellation(context.CancellationToken).ConfigureAwait(false))");
                                sb.AppendLine("                    yield return item;");
                            }
                            else if (e.Kind == HandlerKind.Command)
                            {
                                sb.AppendLine("                await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                                sb.AppendLine("                return new global::Google.Protobuf.WellKnownTypes.Empty();");
                            }
                            else
                            {
                                sb.AppendLine("                var result = await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
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
            foreach (var handler in items
                .Select(HandlerService)
                .Distinct(StringComparer.Ordinal))
            {
                var contract = items
                    .First(item => HandlerService(item) == handler)
                    .TypeFullName;
                sb.AppendLine("            VerifyGrpcHandlerRegistration(app.ServiceProvider, typeof(" + handler + "), " + Literal(contract) + ", missingHandlers);");
            }
            sb.AppendLine("            if (missingHandlers.Count > 0)");
            sb.AppendLine("                throw new global::System.InvalidOperationException(\"Missing mediator handler registrations: \" + string.Join(\"; \", missingHandlers));");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup))
                    for (var version = 1; version <= maxVersion; version++)
                        if (group.Any(e => IsGrpcActive(e, version)))
                            sb.AppendLine("            global::Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions.MapGrpcService<" + Identifier(group.Key) + "V" + version + "GrpcService>(app);");
            }
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void VerifyGrpcHandlerRegistration(global::System.IServiceProvider services, global::System.Type handlerType, string contract, global::System.Collections.Generic.List<string> missingHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            var container = services.GetService(typeof(global::SimpleInjector.Container)) as global::SimpleInjector.Container;");
            sb.AppendLine("            if (container is not null ? container.GetRegistration(handlerType) is null : services.GetService(handlerType) is null)");
            sb.AppendLine("                missingHandlers.Add(contract + \" -> \" + handlerType);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            EmitProtoAssets(sb, items, compilation);
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Grpc.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string HandlerService(EndpointModel item)
        {
            return item.Kind == HandlerKind.Query
                ? "global::Ark.Tools.Solid.IQueryHandler<" + item.TypeFullName + ", " + item.Response + ">"
                : item.Kind == HandlerKind.Command
                    ? "global::Ark.Tools.Solid.ICommandHandler<" + item.TypeFullName + ">"
                    : "global::Ark.Tools.Solid.IRequestHandler<" + item.TypeFullName + ", " + item.Response + ">";
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
            Compilation compilation)
        {
            sb.AppendLine("    /// <summary>Source-generated protobuf assets for the discovered gRPC contracts.</summary>");
            sb.AppendLine("    public static class ArkGeneratedProtos");
            sb.AppendLine("    {");
            var contracts = GetProtoContracts(compilation);
            var entries = new List<string>();
            var content = new StringBuilder();
            foreach (var group in items.GroupBy(static item => item.ServiceGroup).OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                var active = group.ToArray();
                var requestNames = active
                    .Select(item => ProtoTypeName(item.TypeFullName, contracts))
                    .ToHashSet(StringComparer.Ordinal);
                var reachable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var endpoint in active)
                {
                    AddReachable(endpoint.TypeFullName, contracts, reachable);
                    AddReachable(endpoint.IsStreaming ? endpoint.StreamElement! : endpoint.Response, contracts, reachable);
                }

                content.Clear();
                content.AppendLine("syntax = \"proto3\";");
                content.AppendLine();
                content.Append("option csharp_namespace = ")
                    .Append(Literal(GetProtoNamespace(compilation)))
                    .AppendLine(";");
                content.AppendLine();
                content.AppendLine("import \"google/type/date.proto\";");
                content.AppendLine("import \"google/type/datetime.proto\";");
                content.AppendLine("import \"google/protobuf/empty.proto\";");
                if (reachable.Any(type => contracts
                    .FirstOrDefault(contract => SymbolEqualityComparer.Default.Equals(contract.Type, type))?
                    .Members.Any(member => IsArkNodaTimePeriod(member.Type)) == true))
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
                    .Where(contract => reachable.Contains(contract.Type))
                    .OrderBy(static contract => contract.Name, StringComparer.Ordinal))
                    EmitProtoMessage(content, contract, contracts, requestNames.Contains(contract.Name));

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
                                : "(" + ProtoTypeName(item.TypeFullName, contracts) + ") returns ");
                        if (item.AttachmentResponse)
                            content.Append("(stream DownloadDocumentChunk);");
                        else if (item.IsStreaming)
                            content.Append("(stream ").Append(ProtoTypeName(item.StreamElement!, contracts)).Append(");");
                        else
                            content.Append('(').Append(ProtoTypeName(item.Response, contracts)).Append(");");
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
            var assemblyName = compilation.AssemblyName ?? "Ark.MediatorFramework";
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
            IReadOnlyList<ProtoContractModel> contracts,
            bool isRequest)
        {
            WriteComment(sb, contract.Summary);
            sb.Append("message ").Append(contract.Name).AppendLine(" {");
            foreach (var include in contract.Includes)
            {
                sb.Append("  ").Append(SimpleName(include.TypeName)).Append(' ')
                    .Append(SnakeCase(SimpleName(include.TypeName))).Append(" = ")
                    .Append(include.Number).AppendLine(";");
            }

            foreach (var member in contract.Members
                .Where(member => !isRequest || (!member.IsServerSet
                    && !member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Contains("IArkAttachment", StringComparison.Ordinal)))
                .OrderBy(static member => member.Number))
            {
                WriteComment(sb, member.Description);
                var type = ProtoTypeName(member.Type, contracts);
                sb.Append("  ");
                if (member.IsRepeated)
                    sb.Append("repeated ");
                sb.Append(type).Append(' ').Append(SnakeCase(member.Name)).Append(" = ")
                    .Append(member.Number).AppendLine(";");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static IReadOnlyList<ProtoContractModel> GetProtoContracts(Compilation compilation)
        {
            var protoAttribute = compilation.GetTypeByMetadataName("ProtoBuf.ProtoContractAttribute");
            if (protoAttribute is null)
                return Array.Empty<ProtoContractModel>();

            var result = new List<ProtoContractModel>();
            foreach (var assembly in _relevantAssemblies(compilation, protoAttribute.ContainingAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace)
                    .Where(type => type.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, protoAttribute))))
                {
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
                            item.Property.Type,
                            XmlDocumentation.Summary(item.Property),
                            item.Attribute!.ConstructorArguments.FirstOrDefault().Value is int number ? number : 0,
                            item.Property.Type is IArrayTypeSymbol
                                || item.Property.Type is INamedTypeSymbol named
                                    && named.IsGenericType
                                && named.Name == "IReadOnlyList",
                        item.Property.GetAttributes().Any(attribute =>
                            attribute.AttributeClass?.ToDisplayString() == ServerSetAttribute)))
                        .Where(member => member.Number > 0)
                        .ToArray();

                    var includes = type.GetAttributes()
                        .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "ProtoBuf.ProtoIncludeAttribute")
                        .Select(attribute => new
                        {
                            Type = attribute.ConstructorArguments.ElementAtOrDefault(1).Value as INamedTypeSymbol,
                            Number = attribute.ConstructorArguments.FirstOrDefault().Value is int number ? number : 0,
                        })
                        .Where(include => include.Type is not null && include.Number > 0)
                        .Select(include => new ProtoIncludeModel(include.Type!, include.Number))
                        .ToArray();

                    var name = protoContract.NamedArguments
                        .FirstOrDefault(argument => argument.Key == "Name")
                        .Value.Value as string;
                    result.Add(new ProtoContractModel(
                        type,
                        string.IsNullOrWhiteSpace(name) ? GeneratedName(type) : name!,
                        XmlDocumentation.Summary(type),
                        members,
                        includes));
                }
            }
            return result;
        }

        private static void AddReachable(
            string displayName,
            IReadOnlyList<ProtoContractModel> contracts,
            ISet<INamedTypeSymbol> reachable)
        {
            var name = SimpleName(displayName);
            var contract = contracts.FirstOrDefault(item =>
                item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == displayName)
                ?? contracts.FirstOrDefault(item => item.Name == name);
            if (contract is null || !reachable.Add(contract.Type))
                return;

            foreach (var member in contract.Members)
                AddReachable(member.Type, contracts, reachable);
            foreach (var include in contract.Includes)
                AddReachable(include.TypeName, contracts, reachable);
        }

        private static void AddReachable(
            ITypeSymbol type,
            IReadOnlyList<ProtoContractModel> contracts,
            ISet<INamedTypeSymbol> reachable)
            => AddReachable(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), contracts, reachable);

        private static string ProtoTypeName(ITypeSymbol type, IReadOnlyList<ProtoContractModel> contracts)
        {
            if (type is IArrayTypeSymbol array)
                return ProtoTypeName(array.ElementType, contracts);
            if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "Nullable")
                return ProtoTypeName(named.TypeArguments[0], contracts);
            if (type is INamedTypeSymbol evolvableEnum && IsEvolvableEnum(evolvableEnum))
                return EvolvableEnumProtoType(evolvableEnum);

            var contract = contracts.FirstOrDefault(item => SymbolEqualityComparer.Default.Equals(item.Type, type));
            if (contract is not null)
                return contract.Name;

            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                    return "string";
                case SpecialType.System_Boolean:
                    return "bool";
                case SpecialType.System_Int64:
                    return "int64";
                case SpecialType.System_UInt64:
                    return "uint64";
                case SpecialType.System_Int32:
                case SpecialType.System_Int16:
                case SpecialType.System_Byte:
                    return "int32";
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt16:
                    return "uint32";
                case SpecialType.System_Single:
                    return "float";
                case SpecialType.System_Double:
                    return "double";
            }

            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return name switch
            {
                "global::System.String" => "string",
                "global::System.Guid" => "bytes",
                "global::System.Boolean" => "bool",
                "global::System.Int64" => "int64",
                "global::System.UInt64" => "uint64",
                "global::System.Int32" or "global::System.Int16" or "global::System.Byte" => "int32",
                "global::System.UInt32" or "global::System.UInt16" => "uint32",
                "global::System.Single" => "float",
                "global::System.Double" => "double",
                "global::NodaTime.LocalDate" => "google.type.Date",
                "global::NodaTime.LocalDateTime" => "google.type.DateTime",
                "global::NodaTime.OffsetDateTime" => "google.type.DateTime",
                "global::NodaTime.ZonedDateTime" => "google.type.DateTime",
                "global::NodaTime.Period" => "ark.nodatime.Period",
                "global::Google.Protobuf.WellKnownTypes.Empty" => "google.protobuf.Empty",
                _ when type.TypeKind == TypeKind.Enum => type.Name,
                _ => "bytes",
            };
        }

        private static string ProtoTypeName(string typeName, IReadOnlyList<ProtoContractModel> contracts)
        {
            var name = typeName switch
            {
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

            var contract = contracts.FirstOrDefault(item => item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == typeName);
            return contract?.Name ?? SimpleName(typeName);
        }

        private static bool IsArkNodaTimePeriod(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
                return IsArkNodaTimePeriod(array.ElementType);

            if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "Nullable")
                return IsArkNodaTimePeriod(named.TypeArguments[0]);

            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::NodaTime.Period";
        }

        private static string SimpleName(string value)
        {
            var separator = value.LastIndexOf('.');
            return separator < 0 ? value : value[(separator + 1)..];
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
            foreach (var line in text!.Split('\n'))
            {
                var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var current = new StringBuilder();
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

        private readonly record struct EndpointModel
        {
            public EndpointModel(string typeFullName, string typeName, string grpcMethod, string serviceGroup, string response, string? summary, string? remarks, HandlerKind kind, int grpcIntroducedIn, int grpcRetiredIn, bool attachmentResponse, string? streamElement, AttachmentRequestKind attachmentRequest, string? attachmentPropertyName, IReadOnlyList<DiagnosticInfo> diagnostics, Location? location)
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
                Location = location;
                IsValid = diagnostics.Count == 0;
            }

            private EndpointModel(INamedTypeSymbol type, DiagnosticInfo diagnostic)
            {
                TypeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                TypeName = GeneratedName(type);
                Diagnostics = new[] { diagnostic };
                Location = diagnostic.Location;
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
            public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }
            public Location? Location { get; }
            public bool IsValid { get; }
        }

        private readonly record struct DiagnosticInfo
        {
            public DiagnosticInfo(DiagnosticDescriptor descriptor, string typeName, Location location)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = new object[] { typeName };
            }

            public DiagnosticDescriptor Descriptor { get; }
            public Location Location { get; }
            public object[] Arguments { get; }
        }

        private static string GeneratedName(INamedTypeSymbol type)
        {
            var names = new Stack<string>();
            for (var current = type; current is not null; current = current.ContainingType)
                names.Push(current.Name);
            return string.Join("_", names);
        }

        private sealed class ProtoContractModel
        {
            public ProtoContractModel(
                INamedTypeSymbol type,
                string name,
                string? summary,
                IReadOnlyList<ProtoMemberModel> members,
                IReadOnlyList<ProtoIncludeModel> includes)
            {
                Type = type;
                Name = name;
                Summary = summary;
                Members = members;
                Includes = includes;
            }

            public INamedTypeSymbol Type { get; }
            public string Name { get; }
            public string? Summary { get; }
            public IReadOnlyList<ProtoMemberModel> Members { get; }
            public IReadOnlyList<ProtoIncludeModel> Includes { get; }
        }

        private readonly struct ProtoMemberModel
        {
            public ProtoMemberModel(string name, ITypeSymbol type, string? description, int number, bool isRepeated, bool isServerSet)
            {
                Name = name;
                Type = type;
                Description = description;
                Number = number;
                IsRepeated = isRepeated;
                IsServerSet = isServerSet;
            }

            public string Name { get; }
            public ITypeSymbol Type { get; }
            public string? Description { get; }
            public int Number { get; }
            public bool IsRepeated { get; }
            public bool IsServerSet { get; }
        }

        private readonly struct ProtoIncludeModel
        {
            public ProtoIncludeModel(INamedTypeSymbol type, int number)
            {
                Type = type;
                Number = number;
            }

            public INamedTypeSymbol Type { get; }
            public int Number { get; }
            public string TypeName => Type.Name;
        }
    }
}
