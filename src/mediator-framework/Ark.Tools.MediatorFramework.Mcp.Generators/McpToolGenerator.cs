// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.MediatorFramework.Mcp.Generators;

/// <summary>Generates explicit MCP tool adapters for marked mediator contracts.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class McpToolGenerator : IIncrementalGenerator
{
    private const string MarkerAttribute = "Ark.Tools.MediatorFramework.Mcp.ArkGenerateMcpToolsForAssemblyAttribute";
    private const string ToolAttribute = "Ark.Tools.MediatorFramework.McpToolAttribute";
    private const string Query1 = "Ark.Tools.Solid.IQuery`1";
    private const string Query2 = "Ark.Tools.Solid.IQuery`2";
    private const string Request1 = "Ark.Tools.Solid.IRequest`1";
    private const string Request2 = "Ark.Tools.Solid.IRequest`2";
    private const string Command = "Ark.Tools.Solid.ICommand";
    private const string GenericCommand = "Ark.Tools.Solid.ICommand`1";

    private static readonly DiagnosticDescriptor InvalidName = new(
        "ARKMF030", "Invalid MCP tool name", "MCP tool name '{0}' is invalid",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "ARKMF031", "Duplicate MCP tool name", "MCP tool name '{0}' is declared more than once",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor UnsupportedContract = new(
        "ARKMF032", "Unsupported MCP contract", "MCP contract '{0}' is not a supported request, query, or command",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor UnsupportedMember = new(
        "ARKMF033", "Unsupported MCP input member", "MCP contract '{0}' has unsupported input member '{1}'",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingDescription = new(
        "ARKMF037", "Missing MCP description", "MCP tool '{0}' has no XML or explicit description",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Warning, true);
    private static readonly DiagnosticDescriptor InvalidAttachment = new(
        "ARKMF039", "Unsupported MCP attachment", "MCP contract '{0}' has an unsupported attachment member '{1}'",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var markers = context.SyntaxProvider.ForAttributeWithMetadataName(
                MarkerAttribute,
                static (_, _) => true,
                static (attributeContext, _) => GetMarker(attributeContext))
            .Where(static marker => marker is not null)
            .Select(static (marker, _) => marker!);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(markers.Collect()),
            static (sourceProductionContext, input) => Emit(sourceProductionContext, input.Left, input.Right));
    }

    private static MarkerModel? GetMarker(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return null;

        var declaration = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as TypeDeclarationSyntax;
        if (declaration is null || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return new MarkerModel(type, null, context.Attributes[0].ApplicationSyntaxReference?.GetSyntax().GetLocation());

        var marker = context.Attributes[0].ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
        return new MarkerModel(type, marker?.ContainingAssembly?.Name, null);
    }

    private static void Emit(SourceProductionContext context, Compilation compilation, ImmutableArray<MarkerModel> markers)
    {
        foreach (var marker in markers.Distinct(MarkerComparer.Instance))
        {
            if (marker.InvalidLocation is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("ARKMF036", "Invalid MCP marker", "MCP context must be partial",
                        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true),
                    marker.InvalidLocation));
                continue;
            }

            var contracts = FindContracts(compilation, marker.AssemblyName, context.CancellationToken)
                .Select(contract => CreateModel(contract, compilation, context))
                .Where(static model => model is not null)
                .Select(static model => model!)
                .OrderBy(static model => model.Name, StringComparer.Ordinal)
                .ToImmutableArray();

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contract in contracts)
            {
                if (!names.Add(contract.Name))
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateName, contract.Location, contract.Name));
            }

            var source = Render(marker.Context, contracts);
            context.AddSource(marker.Context.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + ".Mcp.g.cs", source);
        }
    }

    private static IEnumerable<INamedTypeSymbol> FindContracts(
        Compilation compilation,
        string? assemblyName,
        CancellationToken cancellationToken)
    {
        if (assemblyName is null)
            return [];

        var assemblies = new List<IAssemblySymbol>();
        if (string.Equals(compilation.AssemblyName, assemblyName, StringComparison.Ordinal))
            assemblies.Add(compilation.Assembly);
        assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols
            .Where(assembly => string.Equals(assembly.Name, assemblyName, StringComparison.Ordinal)));

        return assemblies.SelectMany(assembly => AllTypes(assembly.GlobalNamespace, cancellationToken))
            .Where(type => type.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == ToolAttribute));
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol namespaceSymbol, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in AllNestedTypes(type, cancellationToken))
                yield return nested;
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        foreach (var type in AllTypes(child, cancellationToken))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> AllNestedTypes(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var child in AllNestedTypes(nested, cancellationToken))
                yield return child;
        }
    }

    private static ContractModel? CreateModel(
        INamedTypeSymbol type,
        Compilation compilation,
        SourceProductionContext context)
    {
        var toolAttribute = type.GetAttributes().First(attribute =>
            attribute.AttributeClass?.ToDisplayString() == ToolAttribute);
        var kind = GetHandlerKind(type, out var responseType);
        var location = toolAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        var contractName = type.Name;
        if (kind is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedContract, location, contractName));
            return null;
        }

        var name = GetString(toolAttribute, "Name") ?? contractName;
        var version = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString()
                == "Ark.Tools.MediatorFramework.VersioningAttribute");
        if (version is not null && GetInt(version, "Introduced") is var introduced && introduced > 0)
            name = name.EndsWith("v" + introduced, StringComparison.Ordinal) ? name : name + ".v" + introduced;

        if (name.Length is 0 or > 128 || name.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidName, location, name));
            return null;
        }

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
            .OrderBy(property => property.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray();
        foreach (var property in properties)
        {
            if (property.IsIndexer || property.SetMethod is null && !HasConstructorParameter(type, property))
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(), contractName, property.Name));
            if (IsAttachment(property.Type))
                context.ReportDiagnostic(Diagnostic.Create(InvalidAttachment, property.Locations.FirstOrDefault(), contractName, property.Name));
        }

        var description = GetString(toolAttribute, "Description") ?? XmlDocumentation(type, "remarks");
        var title = GetString(toolAttribute, "Title") ?? XmlDocumentation(type, "summary");
        if (description is null && title is null)
            context.ReportDiagnostic(Diagnostic.Create(MissingDescription, location, name));

        return new ContractModel(
            type,
            name,
            title,
            description,
            GetBool(toolAttribute, "ReadOnly", kind == HandlerKind.Query),
            GetBool(toolAttribute, "Destructive", kind != HandlerKind.Query),
            GetBool(toolAttribute, "Idempotent", false),
            GetBool(toolAttribute, "OpenWorld", true),
            kind.Value,
            responseType!,
            properties,
            location);
    }

    private static bool HasConstructorParameter(INamedTypeSymbol type, IPropertySymbol property)
        => type.Constructors.Any(constructor => constructor.Parameters.Any(parameter =>
            string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)));

    private static bool IsAttachment(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Contains("Ark.Tools.MediatorFramework.IArkAttachment", StringComparison.Ordinal);

    private static HandlerKind? GetHandlerKind(INamedTypeSymbol type, out ITypeSymbol? responseType)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            var metadataName = @interface.OriginalDefinition.ContainingNamespace.ToDisplayString()
                + "." + @interface.OriginalDefinition.MetadataName;
            if (metadataName is Query1 or Query2 or Request1 or Request2)
            {
                responseType = @interface.TypeArguments[^1];
                return metadataName is Query1 or Query2 ? HandlerKind.Query : HandlerKind.Request;
            }
            if (metadataName is Command or GenericCommand)
            {
                responseType = null;
                return HandlerKind.Command;
            }
        }

        responseType = null;
        return type.AllInterfaces.Any(@interface => @interface.OriginalDefinition.ContainingNamespace.ToDisplayString()
            + "." + @interface.OriginalDefinition.MetadataName == Command)
            ? HandlerKind.Command
            : null;
    }

    private static string? XmlDocumentation(ISymbol symbol, string element)
    {
        var xml = symbol.GetDocumentationCommentXml() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        var start = "<" + element + ">";
        var end = "</" + element + ">";
        var startIndex = xml.IndexOf(start, StringComparison.Ordinal);
        var endIndex = xml.IndexOf(end, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
            return null;
        var value = xml.Substring(startIndex + start.Length, endIndex - startIndex - start.Length);
        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? GetString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value as string;

    private static bool GetBool(AttributeData attribute, string name, bool fallback)
        => attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value is bool value ? value : fallback;

    private static int GetInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value is int value ? value : 0;

    private static string Render(INamedTypeSymbol contextType, ImmutableArray<ContractModel> contracts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.Append("namespace ").Append(contextType.ContainingNamespace.ToDisplayString()).AppendLine(";");
        builder.AppendLine();
        builder.Append("partial class ").Append(contextType.Name).AppendLine();
        builder.AppendLine("{");
        builder.AppendLine("    internal static global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder RegisterMcpTools(global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder builder)");
        builder.AppendLine("    {");
        builder.AppendLine("        return builder");
        for (var index = 0; index < contracts.Length; index++)
            builder.Append("            .WithTool(Tool").Append(index).Append("())").AppendLine(index == contracts.Length - 1 ? ";" : string.Empty);
        if (contracts.Length == 0)
            builder.AppendLine("            ;");
        builder.AppendLine("    }");
        for (var index = 0; index < contracts.Length; index++)
            RenderTool(builder, contracts[index], index);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void RenderTool(StringBuilder builder, ContractModel model, int index)
    {
        var response = model.ResponseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var returnType = model.Kind == HandlerKind.Command ? "global::System.Threading.Tasks.Task" : "global::System.Threading.Tasks.Task<" + response + ">";
        var parameters = model.Properties.Select(property =>
            "[global::System.ComponentModel.Description(\"" + Escape(XmlDocumentation(property, "summary") ?? string.Empty) + "\")] "
            + property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + " " + ToParameterName(property.Name));
        var delegateTypes = model.Properties.Select(property => property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("global::System.IServiceProvider")
            .Append("global::System.Threading.CancellationToken")
            .Append(returnType);
        builder.AppendLine();
        builder.Append("    private static global::ModelContextProtocol.Server.McpServerTool Tool").Append(index).AppendLine("()");
        builder.AppendLine("    {");
        builder.Append("        return global::ModelContextProtocol.Server.McpServerTool.Create((global::System.Func<")
            .Append(string.Join(", ", delegateTypes)).Append(">)Invoke").Append(index).AppendLine(", new global::ModelContextProtocol.Server.McpServerToolCreateOptions");
        builder.AppendLine("        {");
        builder.Append("            Name = \"").Append(Escape(model.Name)).AppendLine("\",");
        if (model.Title is not null)
            builder.Append("            Title = \"").Append(Escape(model.Title)).AppendLine("\",");
        if (model.Description is not null)
            builder.Append("            Description = \"").Append(Escape(model.Description)).AppendLine("\",");
        builder.Append("            ReadOnly = ").Append(model.ReadOnly ? "true" : "false").AppendLine(",");
        builder.Append("            Destructive = ").Append(model.Destructive ? "true" : "false").AppendLine(",");
        builder.Append("            Idempotent = ").Append(model.Idempotent ? "true" : "false").AppendLine(",");
        builder.Append("            OpenWorld = ").Append(model.OpenWorld ? "true" : "false").AppendLine(",");
        builder.AppendLine("            UseStructuredContent = true");
        builder.AppendLine("        });");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    private static async ").Append(returnType).Append(" Invoke").Append(index).Append("(")
            .Append(string.Join(", ", parameters)).Append(model.Properties.Length > 0 ? ", " : string.Empty)
            .Append("global::System.IServiceProvider services, global::System.Threading.CancellationToken cancellationToken)").AppendLine();
        builder.AppendLine("    {");
        builder.Append("        var request = new ").Append(model.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var settable = model.Properties.Where(property => property.SetMethod is not null).ToImmutableArray();
        if (settable.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("        {");
            foreach (var property in settable)
                builder.Append("            ").Append(property.Name).Append(" = ").Append(ToParameterName(property.Name)).AppendLine(",");
            builder.AppendLine("        };");
        }
        else
        {
            var constructor = model.Type.Constructors
                .OrderBy(candidate => candidate.Parameters.Length)
                .FirstOrDefault(candidate => candidate.Parameters.All(parameter =>
                    model.Properties.Any(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))));
            builder.Append("(").Append(string.Join(", ", constructor?.Parameters.Select(parameter =>
                ToParameterName(model.Properties.First(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).Name))
                ?? [])).AppendLine(");");
        }
        builder.Append("        var container = services.GetRequiredService<global::SimpleInjector.Container>();").AppendLine();
        if (model.Kind == HandlerKind.Query)
            builder.Append("        return await container.GetInstance<global::Ark.Tools.Solid.IQueryProcessor>().ExecuteAsync<")
                .Append(response).Append(">(request, cancellationToken).ConfigureAwait(false);").AppendLine();
        else if (model.Kind == HandlerKind.Request)
            builder.Append("        return await container.GetInstance<global::Ark.Tools.Solid.IRequestProcessor>().ExecuteAsync<")
                .Append(response).Append(">(request, cancellationToken).ConfigureAwait(false);").AppendLine();
        else
            builder.AppendLine("        await container.GetInstance<global::Ark.Tools.Solid.ICommandProcessor>().ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine("    }");
    }

    private static string ToParameterName(string name)
        => char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed record MarkerModel(INamedTypeSymbol Context, string? AssemblyName, Location? InvalidLocation);
    private sealed record ContractModel(
        INamedTypeSymbol Type,
        string Name,
        string? Title,
        string? Description,
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld,
        HandlerKind Kind,
        ITypeSymbol? ResponseType,
        ImmutableArray<IPropertySymbol> Properties,
        Location? Location);
    private enum HandlerKind { Query, Request, Command }

    private sealed class MarkerComparer : IEqualityComparer<MarkerModel>
    {
        public static MarkerComparer Instance { get; } = new();
        public bool Equals(MarkerModel? x, MarkerModel? y)
            => SymbolEqualityComparer.Default.Equals(x?.Context, y?.Context);
        public int GetHashCode(MarkerModel obj)
            => SymbolEqualityComparer.Default.GetHashCode(obj.Context);
    }
}
