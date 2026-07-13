// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ark.MediatorFramework.Generators
{
    /// <summary>
    /// Incremental generator that discovers <c>Ark.Tools.Solid</c> requests/queries decorated with
    /// <c>[GrpcMethod]</c> and emits code-first gRPC service contracts plus <c>MapArkGrpcServices</c>
    /// inside a <c>partial ArkGeneratedEndpoints</c> class. Only the gRPC transport is emitted by
    /// this generator; add <c>Ark.Tools.MediatorFramework.MinimalApi.Generators</c> for HTTP and
    /// <c>Ark.Tools.MediatorFramework.Rebus.Generators</c> for Rebus.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ArkGrpcEndpointGenerator : IIncrementalGenerator
    {
        private const string GrpcMethodAttribute = "Ark.MediatorFramework.GrpcMethodAttribute";
        private const string ServiceGroupAttribute = "Ark.MediatorFramework.ServiceGroupAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var endpoints = context.CompilationProvider
                .SelectMany(static (compilation, _) => GetEndpoints(compilation));

            var collected = endpoints.Collect();

            context.RegisterSourceOutput(
                collected.Combine(context.CompilationProvider),
                static (spc, pair) => Emit(spc, pair.Left, pair.Right));
        }

        private static ImmutableArray<EndpointModel> GetEndpoints(Compilation compilation)
        {
            var grpcAttr = compilation.GetTypeByMetadataName(GrpcMethodAttribute);
            var serviceGroupAttr = compilation.GetTypeByMetadataName(ServiceGroupAttribute);
            if (grpcAttr is null)
                return ImmutableArray<EndpointModel>.Empty;

            var runtimeAssembly = grpcAttr.ContainingAssembly;
            var builder = ImmutableArray.CreateBuilder<EndpointModel>();

            foreach (var assembly in _relevantAssemblies(compilation, runtimeAssembly))
            {
                foreach (var type in _allTypes(assembly.GlobalNamespace))
                {
                    var attrs = type.GetAttributes();
                    var grpc = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, grpcAttr));
                    if (grpc is null)
                        continue;

                    var serviceGroup = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serviceGroupAttr));
                    var model = Extract(type, grpc, serviceGroup);
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
                }
            }
        }

        private static EndpointModel? Extract(INamedTypeSymbol type, AttributeData grpc, AttributeData? serviceGroup)
        {
            string? response = null;
            var kind = HandlerKind.None;

            foreach (var iface in type.AllInterfaces)
            {
                var def = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def == "global::Ark.Tools.Solid.IRequest<TResponse>")
                {
                    kind = HandlerKind.Request;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    break;
                }

                if (def == "global::Ark.Tools.Solid.IQuery<TResult>")
                {
                    kind = HandlerKind.Query;
                    response = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    break;
                }
            }

            if (kind == HandlerKind.None || response is null)
                return null;

            var grpcMethod = grpc.ConstructorArguments.FirstOrDefault().Value as string ?? type.Name;
            var grpcIntroducedIn = NamedInt(grpc, "IntroducedIn", 1);
            var grpcRetiredIn = NamedInt(grpc, "RetiredIn", 0);
            var group = serviceGroup?.ConstructorArguments.FirstOrDefault().Value as string ?? "Ark";

            return new EndpointModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.Name,
                grpcMethod,
                group,
                response,
                kind,
                grpcIntroducedIn,
                grpcRetiredIn);
        }

        private static int NamedInt(AttributeData attribute, string name, int defaultValue)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
            return argument.Value.Value is int value ? value : defaultValue;
        }

        private static void Emit(
            SourceProductionContext spc,
            ImmutableArray<EndpointModel> items,
            Compilation compilation)
        {
            if (items.IsDefaultOrEmpty)
                return;

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
                            sb.AppendLine("            /// <summary>Dispatches " + e.TypeName + " to its pure handler.</summary>");
                            sb.AppendLine("            [global::System.ServiceModel.OperationContract(Name = " + Literal(e.GrpcMethod) + ")]");
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
                                : "global::Ark.Tools.Solid.IRequestHandler<" + e.TypeFullName + ", " + e.Response + ">";
                            sb.AppendLine("            /// <inheritdoc />");
                            sb.AppendLine("            public async global::System.Threading.Tasks.ValueTask<" + e.Response + "> " + e.TypeName + "Async(" + e.TypeFullName + " request, global::ProtoBuf.Grpc.CallContext context = default)");
                            sb.AppendLine("            {");
                            sb.AppendLine("                var handler = _container.GetInstance<" + handlerService + ">();");
                            sb.AppendLine("                return await handler.ExecuteAsync(request, context.CancellationToken).ConfigureAwait(false);");
                            sb.AppendLine("            }");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }
                }
            }

            // MapArkGrpcServices is always emitted so callers can unconditionally invoke it.
            sb.AppendLine("        /// <summary>Maps every generated code-first gRPC service.</summary>");
            sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapArkGrpcServices(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
            sb.AppendLine("        {");
            if (!items.IsDefaultOrEmpty)
            {
                foreach (var group in items.GroupBy(static x => x.ServiceGroup))
                    for (var version = 1; version <= maxVersion; version++)
                        if (group.Any(e => IsGrpcActive(e, version)))
                            sb.AppendLine("            global::Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions.MapGrpcService<" + Identifier(group.Key) + "V" + version + "GrpcService>(app);");
            }
            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            EmitProtoAssets(sb, items, compilation);
            sb.AppendLine("}");

            spc.AddSource("ArkGeneratedEndpoints.Grpc.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
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
                var reachable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var endpoint in active)
                {
                    AddReachable(endpoint.TypeFullName, contracts, reachable);
                    AddReachable(endpoint.Response, contracts, reachable);
                }

                content.Clear();
                content.AppendLine("syntax = \"proto3\";");
                content.AppendLine();
                content.Append("option csharp_namespace = ")
                    .Append(Literal(GetProtoNamespace(compilation)))
                    .AppendLine(";");
                content.AppendLine();
                content.AppendLine("import \"ark/nodatime.proto\";");
                content.AppendLine("import \"ark/mediator.proto\";");
                content.AppendLine();
                foreach (var contract in contracts
                    .Where(contract => reachable.Contains(contract.Type))
                    .OrderBy(static contract => contract.Name, StringComparer.Ordinal))
                    EmitProtoMessage(content, contract, contracts);

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
                        content.Append("  rpc ").Append(item.GrpcMethod)
                            .Append('(').Append(item.TypeName).Append(") returns (")
                            .Append(SimpleName(item.Response)).AppendLine(");");
                    }
                    content.AppendLine("}");
                    content.AppendLine();
                }

                var fileName = Identifier(group.Key) + ".proto";
                EmitProtoEntry(sb, fileName, content.ToString());
                entries.Add("Get" + Identifier(Path.GetFileNameWithoutExtension(fileName)) + "()");
            }

            var upload = new StringBuilder();
            upload.AppendLine("syntax = \"proto3\";");
            upload.AppendLine();
            upload.Append("option csharp_namespace = ")
                .Append(Literal(GetProtoNamespace(compilation)))
                .AppendLine(";");
            upload.AppendLine();
            upload.AppendLine("import \"ark/mediator.proto\";");
            upload.AppendLine();
            upload.AppendLine("message UploadResponse {");
            upload.AppendLine("  string name = 1;");
            upload.AppendLine("  string content_type = 2;");
            upload.AppendLine("  int64 length = 3;");
            upload.AppendLine("}");
            upload.AppendLine();
            upload.AppendLine("service Documents {");
            upload.AppendLine("  rpc Upload(stream UploadDocumentChunk) returns (UploadResponse);");
            upload.AppendLine("}");
            upload.AppendLine();
            EmitProtoEntry(sb, "Documents.proto", upload.ToString());
            entries.Add("GetDocuments()");
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
            IReadOnlyList<ProtoContractModel> contracts)
        {
            sb.Append("message ").Append(contract.Name).AppendLine(" {");
            foreach (var include in contract.Includes)
            {
                sb.Append("  ").Append(SimpleName(include.TypeName)).Append(' ')
                    .Append(SnakeCase(SimpleName(include.TypeName))).Append(" = ")
                    .Append(include.Number).AppendLine(";");
            }

            foreach (var member in contract.Members.OrderBy(static member => member.Number))
            {
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
                    var members = type.GetMembers()
                        .OfType<IPropertySymbol>()
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
                            item.Attribute!.ConstructorArguments.FirstOrDefault().Value is int number ? number : 0,
                            item.Property.Type is IArrayTypeSymbol
                                || item.Property.Type is INamedTypeSymbol named
                                    && named.IsGenericType
                                    && named.Name == "IReadOnlyList"))
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

                    result.Add(new ProtoContractModel(type, type.Name, members, includes));
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
            var contract = contracts.FirstOrDefault(item => item.Name == name);
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
                "global::NodaTime.LocalDate" => "LocalDate",
                "global::NodaTime.LocalDateTime" => "LocalDateTime",
                "global::NodaTime.OffsetDateTime" => "OffsetDateTime",
                "global::NodaTime.Period" => "Period",
                _ when type.TypeKind == TypeKind.Enum => type.Name,
                _ => "bytes",
            };
        }

        private static string SimpleName(string value)
        {
            var separator = value.LastIndexOf('.');
            return separator < 0 ? value : value[(separator + 1)..];
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
        }

        private readonly struct EndpointModel
        {
            public EndpointModel(string typeFullName, string typeName, string grpcMethod, string serviceGroup, string response, HandlerKind kind, int grpcIntroducedIn, int grpcRetiredIn)
            {
                TypeFullName = typeFullName;
                TypeName = typeName;
                GrpcMethod = grpcMethod;
                ServiceGroup = serviceGroup;
                Response = response;
                Kind = kind;
                GrpcIntroducedIn = grpcIntroducedIn;
                GrpcRetiredIn = grpcRetiredIn;
            }

            public string TypeFullName { get; }
            public string TypeName { get; }
            public string GrpcMethod { get; }
            public string ServiceGroup { get; }
            public string Response { get; }
            public HandlerKind Kind { get; }
            public int GrpcIntroducedIn { get; }
            public int GrpcRetiredIn { get; }
        }

        private sealed class ProtoContractModel
        {
            public ProtoContractModel(
                INamedTypeSymbol type,
                string name,
                IReadOnlyList<ProtoMemberModel> members,
                IReadOnlyList<ProtoIncludeModel> includes)
            {
                Type = type;
                Name = name;
                Members = members;
                Includes = includes;
            }

            public INamedTypeSymbol Type { get; }
            public string Name { get; }
            public IReadOnlyList<ProtoMemberModel> Members { get; }
            public IReadOnlyList<ProtoIncludeModel> Includes { get; }
        }

        private readonly struct ProtoMemberModel
        {
            public ProtoMemberModel(string name, ITypeSymbol type, int number, bool isRepeated)
            {
                Name = name;
                Type = type;
                Number = number;
                IsRepeated = isRepeated;
            }

            public string Name { get; }
            public ITypeSymbol Type { get; }
            public int Number { get; }
            public bool IsRepeated { get; }
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
