// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
    private const string ServerSetAttribute = "Ark.Tools.MediatorFramework.ServerSetAttribute";
    private const string ApiGroupAttribute = "Ark.Tools.MediatorFramework.ApiGroupAttribute";
    private const string HttpEndpointAttribute = "Ark.Tools.MediatorFramework.HttpEndpointAttribute";
    private const string MarkerParserTrackingName = "McpMarkerParser";
    private const string DocumentationParserTrackingName = "McpDocumentationParser";

    private static readonly DiagnosticDescriptor InvalidName = new(
        "ARKMF050", "Use a valid MCP tool name", "MCP tool name '{0}' is invalid; rename the tool to a valid MCP identifier",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF050.md");
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "ARKMF051", "Use a unique MCP tool name", "MCP tool name '{0}' is declared more than once; rename one tool",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF051.md");
    private static readonly DiagnosticDescriptor UnsupportedContract = new(
        "ARKMF052", "Use a supported MCP contract", "MCP contract '{0}' must be a supported request, query, or command",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF052.md");
    private static readonly DiagnosticDescriptor UnsupportedMember = new(
        "ARKMF053", "Use supported MCP input members", "MCP contract '{0}' has unsupported input member '{1}'; remove or change the member",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF053.md");
    private static readonly DiagnosticDescriptor MissingConstructor = new(
        "ARKMF054", "Add the MCP contract constructor", "MCP contract '{0}' must declare a constructor matching its input members",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF054.md");
    private static readonly DiagnosticDescriptor MissingDescription = new(
        "ARKMF055", "Document the MCP tool", "MCP tool '{0}' must have an XML description or DescriptionAttribute",
        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF055.md");
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var markers = context.SyntaxProvider.ForAttributeWithMetadataName(
                MarkerAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => GetMarkers(attributeContext))
            .WithTrackingName(MarkerParserTrackingName)
            .SelectMany(static (markerGroup, _) => markerGroup);
        var documentationFiles = context.AdditionalTextsProvider
            .Where(static text => string.Equals(Path.GetExtension(text.Path), ".xml", StringComparison.OrdinalIgnoreCase))
            .Select(static (text, cancellationToken) => GetDocumentationFile(text, cancellationToken))
            .WithTrackingName(DocumentationParserTrackingName)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(markers.Collect()).Combine(documentationFiles),
            static (sourceProductionContext, input) =>
                Emit(sourceProductionContext, input.Left.Left, input.Left.Right, input.Right));
    }

    private static ImmutableArray<MarkerModel> GetMarkers(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return [];

        var invalidLocation = IsPartial(type) && AllContainingTypesArePartial(type)
            ? null
            : CreateLocation(context.Attributes[0].ApplicationSyntaxReference?.GetSyntax().GetLocation());
        var contextMetadataName = GetMetadataName(type);
        return context.Attributes
            .Select(marker => new MarkerModel(
                contextMetadataName,
                marker.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol markerType
                    ? markerType.ContainingAssembly.Name
                    : null,
                invalidLocation))
            .OrderBy(static marker => marker.AssemblyName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool AllContainingTypesArePartial(INamedTypeSymbol type)
    {
        for (var containingType = type.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType)
        {
            if (!IsPartial(containingType))
                return false;
        }

        return true;
    }

    private static bool IsPartial(INamedTypeSymbol type)
        => type.DeclaringSyntaxReferences.Length > 0
            && type.DeclaringSyntaxReferences.All(static reference =>
                reference.GetSyntax() is TypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType)
            names.Push(current.MetadataName);

        var namespaceName = type.ContainingNamespace;
        return namespaceName.IsGlobalNamespace
            ? string.Join("+", names)
            : namespaceName.ToDisplayString() + "." + string.Join("+", names);
    }

    private static MarkerLocation? CreateLocation(Location? location)
    {
        if (location is null || location.SourceTree is null)
            return null;

        var mappedLineSpan = location.GetMappedLineSpan();
        var filePath = string.IsNullOrEmpty(mappedLineSpan.Path) ? location.SourceTree.FilePath : mappedLineSpan.Path;
        return new MarkerLocation(
            filePath,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            mappedLineSpan.StartLinePosition.Line,
            mappedLineSpan.StartLinePosition.Character,
            mappedLineSpan.EndLinePosition.Line,
            mappedLineSpan.EndLinePosition.Character);
    }

    private static Location ToLocation(MarkerLocation location)
        => Location.Create(
            location.FilePath,
            new TextSpan(location.Start, location.Length),
            new LinePositionSpan(
                new LinePosition(location.StartLine, location.StartCharacter),
                new LinePosition(location.EndLine, location.EndCharacter)));

    private static DocumentationFileModel GetDocumentationFile(AdditionalText text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new DocumentationFileModel(
            NormalizePath(text.Path),
            text.GetText(cancellationToken)?.ToString() ?? string.Empty);
    }

    private static void Emit(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<MarkerModel> markers,
        ImmutableArray<DocumentationFileModel> additionalDocumentationFiles)
    {
        if (markers.IsDefaultOrEmpty)
            return;

        var grouped = markers
            .GroupBy(static marker => marker.ContextMetadataName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToImmutableArray();

        var contractCache = new Dictionary<string, ImmutableArray<INamedTypeSymbol>>(StringComparer.Ordinal);
        var contractTypesByContext = new Dictionary<string, ImmutableArray<INamedTypeSymbol>>(StringComparer.Ordinal);
        var documentationAssemblyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in grouped)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var contractTypes = new List<INamedTypeSymbol>();
            foreach (var assemblyName in group
                .Where(static value => value.AssemblyName is not null)
                .Select(static value => value.AssemblyName!)
                .OrderBy(static assemblyName => assemblyName, StringComparer.Ordinal))
            {
                if (!contractCache.TryGetValue(assemblyName, out var cachedContracts))
                {
                    cachedContracts = FindContracts(compilation, assemblyName, context.CancellationToken);
                    contractCache.Add(assemblyName, cachedContracts);
                }

                contractTypes.AddRange(cachedContracts);
            }

            var distinctContracts = contractTypes
                .GroupBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .Select(grouping => grouping.First())
                .OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray();
            contractTypesByContext.Add(group.Key, distinctContracts);
            foreach (var contract in distinctContracts)
                documentationAssemblyNames.Add(contract.ContainingAssembly.Name);
        }

        var documentationFiles = GetDocumentationFiles(
            compilation,
            additionalDocumentationFiles,
            documentationAssemblyNames);
        foreach (var group in grouped)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var marker = group.First();
            if (marker.InvalidLocation is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("ARKMF056", "Declare the MCP context as partial", "MCP context and containing types must be declared partial",
                        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
                        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF056.md"),
                    ToLocation(marker.InvalidLocation.Value)));
                continue;
            }

            var contextType = compilation.GetTypeByMetadataName(group.Key);
            if (contextType is null)
                continue;

            var contracts = contractTypesByContext[group.Key]
                .Select(contract => CreateModel(contract, compilation, documentationFiles, context))
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

            var source = Render(contextType, contracts);
            context.AddSource(GetHintName(contextType) + ".Mcp.g.cs", source);
        }
    }

    private static string GetHintName(INamedTypeSymbol type)
        => "Mcp_"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

    private static ImmutableArray<INamedTypeSymbol> FindContracts(
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
                attribute.AttributeClass?.ToDisplayString() == ToolAttribute))
            .ToImmutableArray();
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
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, XElement>> documentationFiles,
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
        var apiGroup = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == ApiGroupAttribute);
        var groupName = apiGroup?.ConstructorArguments.FirstOrDefault().Value as string;
        if (!string.IsNullOrWhiteSpace(groupName))
            name = groupName + "." + name;
        var version = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString()
                == "Ark.Tools.MediatorFramework.VersioningAttribute");
        var introduced = version is null ? 1 : GetInt(version, "Introduced");
        var retired = version is null ? 0 : GetInt(version, "Retired");

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
        var invalid = false;
        foreach (var property in properties)
        {
            if (property.IsIndexer || property.SetMethod is null && !HasConstructorParameter(type, property))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(), contractName, property.Name));
                invalid = true;
            }
            if (property.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == ServerSetAttribute))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(), contractName, property.Name));
                invalid = true;
            }
        }
        if (invalid)
            return null;
        var constructor = FindConstructor(type, properties);
        if (constructor is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingConstructor, location, contractName));
            return null;
        }

        var httpEndpoint = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == HttpEndpointAttribute);
        var allowAnonymous = HasNamedArgument(toolAttribute, "AllowAnonymous")
            ? GetBool(toolAttribute, "AllowAnonymous", false)
            : GetBool(httpEndpoint, "AllowAnonymous", false);
        var hasDescription = TryGetDescription(type, out var attributeDescription);
        var summary = hasDescription
            ? attributeDescription
            : XmlDocumentation(type, "summary", documentationFiles)
                ?? XmlDocumentation(type.ContainingType, "summary", documentationFiles);
        var remarks = hasDescription
            ? null
            : XmlDocumentation(type, "remarks", documentationFiles)
                ?? XmlDocumentation(type.ContainingType, "remarks", documentationFiles);
        var description = summary is null
            ? remarks ?? string.Empty
            : remarks is null
                ? summary
                : summary + " " + remarks;
        if (description.Length == 0)
            context.ReportDiagnostic(Diagnostic.Create(MissingDescription, location, name));

        var propertyDescriptions = properties
            .Select(property => (property.Name, Description: TryGetDescription(property, out var description)
                ? description
                : XmlDocumentation(property, "summary", documentationFiles)))
            .Where(item => item.Description is not null)
            .ToImmutableDictionary(item => item.Name, item => item.Description!, StringComparer.Ordinal);

        return new ContractModel(
            type,
            name,
            description.Length == 0 ? null : description,
            introduced,
            retired,
            GetBool(toolAttribute, "ReadOnly", kind == HandlerKind.Query),
            GetBool(toolAttribute, "Destructive", kind != HandlerKind.Query),
            GetBool(toolAttribute, "Idempotent", false),
            GetBool(toolAttribute, "OpenWorld", true),
            allowAnonymous,
            kind.Value,
            responseType!,
            constructor,
            properties,
            propertyDescriptions,
            location);
    }

    private static bool HasConstructorParameter(INamedTypeSymbol type, IPropertySymbol property)
        => type.Constructors.Any(constructor => constructor.Parameters.Any(parameter =>
            string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)));

    private static bool IsAttachment(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && namedType.Name == "IArkAttachment"
            && namedType.ContainingNamespace.ToDisplayString() == "Ark.Tools.MediatorFramework";

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
        return null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, XElement>> GetDocumentationFiles(
        Compilation compilation,
        ImmutableArray<DocumentationFileModel> additionalDocumentationFiles,
        IEnumerable<string> assemblyNames)
    {
        var selectedAssemblyNames = new HashSet<string>(assemblyNames, StringComparer.Ordinal);
        var documentationFiles = new Dictionary<string, IReadOnlyDictionary<string, XElement>>(StringComparer.Ordinal);
        if (selectedAssemblyNames.Count == 0)
            return documentationFiles;

        foreach (var assemblyName in selectedAssemblyNames.OrderBy(static name => name, StringComparer.Ordinal))
        {
            var documentationFile = additionalDocumentationFiles
                .Where(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file.Path),
                    assemblyName,
                    StringComparison.Ordinal))
                .OrderBy(static file => file.Path, StringComparer.Ordinal)
                .Select(static file => (DocumentationFileModel?)file)
                .FirstOrDefault();
            if (!documentationFile.HasValue)
                continue;

            try
            {
                documentationFiles[assemblyName] = GetDocumentationMembers(
                    XDocument.Parse(documentationFile.Value.Content, LoadOptions.None));
            }
            catch (XmlException)
            {
                continue;
            }
        }

        foreach (var reference in compilation.References)
        {
            if (reference is not PortableExecutableReference portableReference
                || portableReference.FilePath is null
                || compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly
                || documentationFiles.ContainsKey(assembly.Name)
                || !selectedAssemblyNames.Contains(assembly.Name))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(portableReference.FilePath);
            if (directory is null)
                continue;

            var assemblyXmlFileName = Path.GetFileName(assembly.Name) + ".xml";
            var candidates = new[]
            {
                Path.ChangeExtension(portableReference.FilePath, ".xml"),
                Path.Join(directory, assemblyXmlFileName),
                Path.GetFullPath(Path.Join(directory, "..", assemblyXmlFileName)),
            };
            var documentationFile = candidates.FirstOrDefault(File.Exists);
            if (documentationFile is null)
                continue;

            try
            {
                documentationFiles[assembly.Name] = GetDocumentationMembers(XDocument.Load(documentationFile));
            }
            catch (IOException)
            {
                continue;
            }
            catch (XmlException)
            {
                continue;
            }
        }

        return documentationFiles;
    }

    private static IReadOnlyDictionary<string, XElement> GetDocumentationMembers(XDocument document)
    {
        var members = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var membersElement = document.Root?.Element("members");
        if (membersElement is not null)
        {
            foreach (var member in membersElement.Elements("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (name is not null && !members.ContainsKey(name))
                    members.Add(name, member);
            }
        }

        return members;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Replace('\\', '/');

    private static string? XmlDocumentation(
        ISymbol? symbol,
        string element,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, XElement>> documentationFiles)
    {
        if (symbol is null)
            return null;

        var xml = symbol.GetDocumentationCommentXml() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(xml)
            && documentationFiles.TryGetValue(symbol.ContainingAssembly.Name, out var documentationFile))
        {
            var documentationId = symbol.GetDocumentationCommentId();
            if (documentationId is not null
                && documentationFile.TryGetValue(documentationId, out var member))
            {
                xml = member.ToString(SaveOptions.DisableFormatting);
            }
        }
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

    private static bool TryGetDescription(ISymbol symbol, out string? description)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() == "System.ComponentModel.DescriptionAttribute");
        description = attribute?.ConstructorArguments.FirstOrDefault().Value as string;
        return attribute is not null;
    }

    private static string? GetString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value as string;

    private static bool GetBool(AttributeData? attribute, string name, bool fallback)
        => attribute?.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value is bool value
            ? value
            : fallback;

    private static bool HasNamedArgument(AttributeData attribute, string name)
        => attribute.NamedArguments.Any(argument => argument.Key == name);

    private static int GetInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value is int value ? value : 0;

    private static string Render(INamedTypeSymbol contextType, ImmutableArray<ContractModel> contracts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("using global::Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        if (!contextType.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(contextType.ContainingNamespace.ToDisplayString()).AppendLine(";");
            builder.AppendLine();
        }

        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var containingType = contextType.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType)
        {
            containingTypes.Push(containingType);
        }

        var indentation = 0;
        while (containingTypes.Count > 0)
        {
            var containingType = containingTypes.Pop();
            builder.Append(' ', indentation * 4)
                .Append(GetPartialTypeDeclaration(containingType))
                .Append(GetTypeConstraints(containingType))
                .AppendLine();
            builder.Append(' ', indentation * 4).AppendLine("{");
            indentation++;
        }

        AppendIndented(builder, RenderContext(contextType, contracts), indentation);
        while (indentation > 0)
        {
            indentation--;
            builder.Append(' ', indentation * 4).AppendLine("}");
        }

        return builder.ToString();
    }

    private static string RenderContext(INamedTypeSymbol contextType, ImmutableArray<ContractModel> contracts)
    {
        var builder = new StringBuilder();
        builder.Append(GetPartialTypeDeclaration(contextType))
            .Append(" : global::Ark.Tools.MediatorFramework.Mcp.IMcpToolContext")
            .Append(GetTypeConstraints(contextType))
            .AppendLine();
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder RegisterMcpTools(global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder builder)");
        builder.AppendLine("    {");
        RenderVersionMap(builder, contracts);
        builder.AppendLine("        return builder");
        for (var index = 0; index < contracts.Length; index++)
            builder.Append("            .WithTools<Tool").Append(index).Append(">()").AppendLine(index == contracts.Length - 1 ? ";" : string.Empty);
        if (contracts.Length == 0)
            builder.AppendLine("            ;");
        builder.AppendLine("    }");
        for (var index = 0; index < contracts.Length; index++)
            RenderTool(builder, contracts[index], index);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GetPartialTypeDeclaration(INamedTypeSymbol type)
    {
        var declaration = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as TypeDeclarationSyntax;
        var modifiers = declaration is null
            ? "partial"
            : string.Join(" ", declaration.Modifiers.Select(static modifier => modifier.ValueText));
        var keyword = declaration is RecordDeclarationSyntax { ClassOrStructKeyword.ValueText: { Length: > 0 } recordKind }
            ? declaration.Keyword.ValueText + " " + recordKind
            : declaration?.Keyword.ValueText ?? "class";
        var name = declaration?.Identifier.ValueText ?? type.Name;
        var typeParameters = declaration?.TypeParameterList?.ToString() ?? string.Empty;
        return modifiers + " " + keyword + " " + name + typeParameters;
    }

    private static string GetTypeConstraints(INamedTypeSymbol type)
    {
        var declaration = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as TypeDeclarationSyntax;
        var constraints = declaration?.ConstraintClauses.ToFullString().Trim() ?? string.Empty;
        return string.IsNullOrEmpty(constraints) ? string.Empty : " " + constraints;
    }

    private static void AppendIndented(StringBuilder builder, string source, int indentation)
    {
        using var reader = new StringReader(source);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            builder.Append(' ', indentation * 4).AppendLine(line);
    }

    private static void RenderVersionMap(StringBuilder builder, ImmutableArray<ContractModel> contracts)
    {
        var maxVersion = contracts.Length == 0
            ? 1
            : contracts.Max(model => Math.Max(model.Introduced, model.Retired));
        builder.AppendLine("        builder.Services.AddSingleton<global::Ark.Tools.MediatorFramework.Mcp.IMcpToolVersionMap>(");
        builder.AppendLine("            new global::Ark.Tools.MediatorFramework.Mcp.McpToolVersionMap(");
        builder.AppendLine("                new global::System.Collections.Generic.Dictionary<int, string[]>");
        builder.AppendLine("                {");
        for (var version = 1; version <= maxVersion; version++)
        {
            builder.Append("                    [").Append(version).Append("] = [");
            builder.Append(string.Join(
                ", ",
                contracts
                    .Where(model => version >= model.Introduced
                        && (model.Retired == 0 || version < model.Retired))
                    .Select(model => "\"" + Escape(model.Name) + "\"")));
            builder.AppendLine("],");
        }
        builder.AppendLine("                },");
        builder.Append("                [");
        builder.Append(string.Join(
            ", ",
            contracts
                .Where(model => model.Retired == 0)
                .Select(model => "\"" + Escape(model.Name) + "\"")));
        builder.AppendLine("]));");
    }

    private static void RenderTool(StringBuilder builder, ContractModel model, int index)
    {
        var response = model.ResponseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var attachmentResponse = model.ResponseType is not null && IsAttachment(model.ResponseType);
        var returnType = attachmentResponse
            ? "global::System.Threading.Tasks.Task<global::ModelContextProtocol.Protocol.EmbeddedResourceBlock>"
            : model.Kind == HandlerKind.Command
                ? "global::System.Threading.Tasks.Task"
                : "global::System.Threading.Tasks.Task<" + response + ">";
        var parameters = model.Properties.Select(property =>
            "[global::System.ComponentModel.Description("
            + Literal(model.PropertyDescriptions.TryGetValue(property.Name, out var propertyDescription)
                ? propertyDescription
                : string.Empty) + ")] "
            + ToInputType(property.Type) + " " + ToParameterName(property.Name));

        builder.AppendLine();
        builder.AppendLine("    [global::ModelContextProtocol.Server.McpServerToolType]");
        builder.Append("    public sealed class Tool").Append(index).AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        [global::ModelContextProtocol.Server.McpServerTool(");
        builder.Append("            Name = \"").Append(Escape(model.Name)).AppendLine("\",");
        builder.Append("            ReadOnly = ").Append(model.ReadOnly ? "true" : "false").AppendLine(",");
        builder.Append("            Destructive = ").Append(model.Destructive ? "true" : "false").AppendLine(",");
        builder.Append("            Idempotent = ").Append(model.Idempotent ? "true" : "false").AppendLine(",");
        builder.Append("            OpenWorld = ").Append(model.OpenWorld ? "true" : "false").AppendLine(",");
        builder.AppendLine("            UseStructuredContent = true");
        builder.AppendLine("        )]");
        if (model.Description is not null)
            builder.Append("        [global::System.ComponentModel.Description(").Append(Literal(model.Description)).AppendLine(")]");
        builder.Append("        [global::Microsoft.AspNetCore.Authorization.")
            .Append(model.AllowAnonymous ? "AllowAnonymousAttribute" : "AuthorizeAttribute")
            .AppendLine("]");
        builder.Append("        public static async ").Append(returnType).Append(" ExecuteAsync(")
            .Append(string.Join(", ", parameters)).Append(model.Properties.Length > 0 ? ", " : string.Empty)
            .Append("global::System.IServiceProvider services, global::System.Threading.CancellationToken cancellationToken)").AppendLine();
        builder.AppendLine("        {");
        builder.Append("            var request = new ").Append(model.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("(");
        builder.Append(string.Join(", ", model.Constructor.Parameters.Select(parameter =>
            ToParameterName(model.Properties.First(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).Name)
            + (IsAttachment(model.Properties.First(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).Type)
                ? ".ToAttachment()" : string.Empty))));
        var settable = model.Properties.Where(property => property.SetMethod is not null).ToImmutableArray();
        if (settable.Length > 0)
        {
            builder.AppendLine(")");
            builder.AppendLine("            {");
            foreach (var property in settable)
                if (!model.Constructor.Parameters.Any(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)))
                    builder.Append("                ").Append(property.Name).Append(" = ").Append(ToInputValue(property)).AppendLine(",");
            builder.AppendLine("            };");
        }
        else
            builder.AppendLine(");");
        if (model.Kind == HandlerKind.Query)
        {
            builder.Append("            var result = await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IQueryProcessor>(services).ExecuteAsync<")
                .Append(response).Append(">(request, cancellationToken).ConfigureAwait(false);").AppendLine();
            if (attachmentResponse)
                builder.AppendLine("            return await global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentResults.ToEmbeddedResourceAsync(result, cancellationToken: cancellationToken).ConfigureAwait(false);");
            else
                builder.AppendLine("            return result;");
        }
        else if (model.Kind == HandlerKind.Request)
        {
            builder.Append("            var result = await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IRequestProcessor>(services).ExecuteAsync<")
                .Append(response).Append(">(request, cancellationToken).ConfigureAwait(false);").AppendLine();
            if (attachmentResponse)
                builder.AppendLine("            return await global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentResults.ToEmbeddedResourceAsync(result, cancellationToken: cancellationToken).ConfigureAwait(false);");
            else
                builder.AppendLine("            return result;");
        }
        else
        {
            builder.AppendLine("            await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.ICommandProcessor>(services).ExecuteAsync(request, cancellationToken).ConfigureAwait(false);");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static string ToParameterName(string name)
        => char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string ToInputType(ITypeSymbol type)
        => IsAttachment(type)
            ? "global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentInput"
            : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string ToInputValue(IPropertySymbol property)
        => IsAttachment(property.Type)
            ? ToParameterName(property.Name) + ".ToAttachment()"
            : ToParameterName(property.Name);

    private static IMethodSymbol? FindConstructor(INamedTypeSymbol type, ImmutableArray<IPropertySymbol> properties)
        => type.Constructors
            .Where(candidate => candidate.Parameters.All(parameter =>
                properties.Any(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))))
            .Where(candidate => properties.Where(property => property.SetMethod is null).All(property =>
                candidate.Parameters.Any(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .FirstOrDefault();

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Literal(string value)
        => SyntaxFactory.Literal(value).ToFullString();

    private readonly record struct DocumentationFileModel(string Path, string Content);
    private sealed record MarkerModel(string ContextMetadataName, string? AssemblyName, MarkerLocation? InvalidLocation);
    private readonly record struct MarkerLocation(
        string FilePath,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter);
    private sealed record ContractModel(
        INamedTypeSymbol Type,
        string Name,
        string? Description,
        int Introduced,
        int Retired,
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld,
        bool AllowAnonymous,
        HandlerKind Kind,
        ITypeSymbol? ResponseType,
        IMethodSymbol Constructor,
        ImmutableArray<IPropertySymbol> Properties,
        ImmutableDictionary<string, string> PropertyDescriptions,
        Location? Location);
    private enum HandlerKind { Query, Request, Command }

}
