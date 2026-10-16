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

using Ark.Tools.MediatorFramework.Generators;

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
    private const string Query1 = "Ark.Tools.Solid.IQuery`1";
    private const string Query2 = "Ark.Tools.Solid.IQuery`2";
    private const string Request1 = "Ark.Tools.Solid.IRequest`1";
    private const string Request2 = "Ark.Tools.Solid.IRequest`2";
    private const string Command = "Ark.Tools.Solid.ICommand";
    private const string GenericCommand = "Ark.Tools.Solid.ICommand`1";
    private const string MarkerParserTrackingName = "McpMarkerParser";
    private const string DocumentationParserTrackingName = "McpDocumentationParser";
    private const string ContractParserTrackingName = "McpContractParser";
    private const string ReferencedContractsTrackingName = "McpReferencedContracts";
    private const string ModelTrackingName = "McpModel";
    private const string EmissionInputTrackingName = "McpEmissionInput";
    // Referenced documentation copied by Ark.Tools.MediatorFramework.Mcp.targets, hidden from generators that read every .xml file.
    private const string ReferencedDocumentationExtension = ".arkmcpdoc";

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
            .SelectMany(static (markerGroup, _) => markerGroup)
            .Collect()
            .Select(static (markerModels, _) => new EquatableArray<MarkerModel>(markerModels));
        var markerAssemblies = markers
            .Select(static (markerModels, _) => new EquatableArray<string>(markerModels
                .Select(static marker => marker.AssemblyName)
                .Where(static assemblyName => assemblyName is not null)
                .Select(static assemblyName => assemblyName!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static assemblyName => assemblyName, StringComparer.Ordinal)
                .ToImmutableArray()));
        var sourceContracts = context.SyntaxProvider.ForAttributeWithMetadataName(
                ContractSymbols.McpToolAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => attributeContext.TargetSymbol is INamedTypeSymbol type
                    ? CreateContract(type, attributeContext.Attributes[0])
                    : null)
            .WithTrackingName(ContractParserTrackingName)
            .Where(static contract => contract is not null)
            .Select(static (contract, _) => contract!)
            .Collect()
            .Select(static (contracts, _) => new EquatableArray<ContractModel>(contracts));
        // Referenced-assembly scans read metadata symbols and documentation files only, so they rerun
        // when references change instead of on every edit.
        var referenceScope = context.CompilationProvider._referencesOnly();
        var referencedContracts = referenceScope
            .Combine(markerAssemblies)
            .Select(static (pair, cancellationToken) => new EquatableArray<AssemblyContracts>(
                FindReferencedContracts(pair.Left, pair.Right.Values, cancellationToken)))
            .WithTrackingName(ReferencedContractsTrackingName);
        var groups = markers
            .Combine(sourceContracts)
            .Combine(referencedContracts)
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName))
            .Select(static (input, cancellationToken) =>
            {
                var (((markerModels, source), referenced), assemblyName) = input;
                return CreateGroups(markerModels.Values, source.Values, referenced.Values, assemblyName, cancellationToken);
            })
            .WithTrackingName(ModelTrackingName);
        var documentationRequest = groups
            .Select(static (model, _) => CreateDocumentationRequest(model));
        // Each relevant XML file is parsed on its own, keeping only the members the emitter looks up,
        // so editing one file or an unrelated one does not reparse the others.
        var additionalDocumentation = context.AdditionalTextsProvider
            .Where(static text => IsDocumentationFile(text.Path))
            .Combine(documentationRequest)
            .Where(static pair => !pair.Right.MemberIds.IsEmpty && pair.Right.AssemblyNames.Values.Contains(
                Path.GetFileNameWithoutExtension(pair.Left.Path), StringComparer.Ordinal))
            .Select(static (pair, cancellationToken) => GetDocumentationFile(pair.Left, pair.Right.MemberIds.Values, cancellationToken))
            .WithTrackingName(DocumentationParserTrackingName)
            .Collect()
            .Select(static (files, _) => new EquatableArray<DocumentationAssembly>(GetAdditionalDocumentation(files)));

        // The emitted source reads no locations or diagnostics, so an edit that only shifts lines leaves the
        // emission input unchanged.
        context.RegisterSourceOutput(
            groups.Select(static (model, _) => model.ForEmission())
                .Combine(additionalDocumentation)
                .WithTrackingName(EmissionInputTrackingName),
            static (sourceProductionContext, input) =>
                Emit(
                    sourceProductionContext,
                    input.Left,
                    input.Right.Values,
                    diagnosticsOnly: false));

        // Diagnostics only: reported at the real source locations so #pragma and EditorConfig suppressions apply.
        context.RegisterSourceOutput(
            groups.Combine(additionalDocumentation),
            static (sourceProductionContext, input) =>
                Emit(
                    sourceProductionContext,
                    input.Left,
                    input.Right.Values,
                    diagnosticsOnly: true));
    }

    private static EquatableArray<MarkerModel> GetMarkers(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
            return EquatableArray<MarkerModel>.Empty;

        var invalidLocation = IsPartial(type) && AllContainingTypesArePartial(type)
            ? null
            : CreateLocation(context.Attributes[0].ApplicationSyntaxReference?.GetSyntax().GetLocation());
        var contextMetadataName = GetMetadataName(type);
        var contextModel = CreateContextModel(type);
        return context.Attributes
            .Select(marker => new MarkerModel(
                contextMetadataName,
                marker.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol markerType
                    ? markerType.ContainingAssembly.Name
                    : null,
                invalidLocation,
                contextModel))
            .OrderBy(static marker => marker.AssemblyName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ContextModel CreateContextModel(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var containingType = type.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType)
        {
            containingTypes.Push(containingType);
        }

        return new ContextModel(
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            containingTypes
                .Select(static containingType => GetPartialTypeDeclaration(containingType) + GetTypeConstraints(containingType))
                .ToImmutableArray(),
            GetPartialTypeDeclaration(type),
            GetTypeConstraints(type),
            GetHintName(type));
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

    private static bool IsDocumentationFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ReferencedDocumentationExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static DocumentationFileModel GetDocumentationFile(
        AdditionalText text,
        ImmutableArray<string> memberIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = NormalizePath(text.Path);
        var assemblyName = Path.GetFileNameWithoutExtension(text.Path);
        try
        {
            var document = XDocument.Parse(text.GetText(cancellationToken)?.ToString() ?? string.Empty, LoadOptions.None);
            return new DocumentationFileModel(path, assemblyName, GetDocumentationMembers(document, memberIds));
        }
        catch (XmlException)
        {
            return new DocumentationFileModel(path, assemblyName, null);
        }
    }

    // Mirrors CreateTool: only members without in-source XML or a DescriptionAttribute are looked up
    // in documentation files by the emitter.
    private static DocumentationRequest CreateDocumentationRequest(McpModel model)
    {
        var memberIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var contract in model.Groups.SelectMany(static group => group.Contracts))
        {
            if (!contract.HasDescriptionAttribute)
            {
                AddDocumentationId(memberIds, contract.Documentation);
                AddDocumentationId(memberIds, contract.ContainingTypeDocumentation);
            }
            foreach (var property in contract.Properties.Where(static property => !property.HasDescriptionAttribute))
                AddDocumentationId(memberIds, property.Documentation);
        }

        return new DocumentationRequest(model.DocumentationAssemblyNames, memberIds.ToImmutableArray());
    }

    private static void AddDocumentationId(SortedSet<string> memberIds, DocumentationSource? source)
    {
        if (source is { DocumentationId: { } documentationId } documentation && string.IsNullOrWhiteSpace(documentation.Xml))
            memberIds.Add(documentationId);
    }

    private static McpModel CreateGroups(
        ImmutableArray<MarkerModel> markers,
        ImmutableArray<ContractModel> sourceContracts,
        ImmutableArray<AssemblyContracts> referencedContracts,
        string? compilationAssemblyName,
        CancellationToken cancellationToken)
    {
        if (markers.IsDefaultOrEmpty)
            return new McpModel(EquatableArray<ContextGroup>.Empty, EquatableArray<string>.Empty);

        var grouped = markers
            .GroupBy(static marker => marker.ContextMetadataName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToImmutableArray();

        var contextGroups = ImmutableArray.CreateBuilder<ContextGroup>(grouped.Length);
        var documentationAssemblyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contracts = new List<ContractModel>();
            foreach (var assemblyName in group
                .Where(static value => value.AssemblyName is not null)
                .Select(static value => value.AssemblyName!)
                .OrderBy(static assemblyName => assemblyName, StringComparer.Ordinal))
            {
                if (string.Equals(compilationAssemblyName, assemblyName, StringComparison.Ordinal))
                    contracts.AddRange(sourceContracts);
                foreach (var referenced in referencedContracts.Where(referenced =>
                    string.Equals(referenced.AssemblyName, assemblyName, StringComparison.Ordinal)))
                {
                    contracts.AddRange(referenced.Contracts);
                }
            }

            var distinctContracts = contracts
                .GroupBy(static contract => contract.TypeFullName, StringComparer.Ordinal)
                .Select(static grouping => grouping.First())
                .OrderBy(static contract => contract.TypeFullName, StringComparer.Ordinal)
                .ToImmutableArray();
            foreach (var contract in distinctContracts)
            {
                documentationAssemblyNames.Add(contract.AssemblyName);
                // An inherited property can be documented in its base type's assembly.
                foreach (var property in contract.Properties)
                    documentationAssemblyNames.Add(property.Documentation.AssemblyName);
            }

            var marker = group.First();
            contextGroups.Add(new ContextGroup(marker.Context, marker.InvalidLocation, distinctContracts));
        }

        return new McpModel(
            contextGroups.MoveToImmutable(),
            documentationAssemblyNames.OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray());
    }

    private static void Emit(
        SourceProductionContext context,
        McpModel model,
        ImmutableArray<DocumentationAssembly> documentation,
        bool diagnosticsOnly)
    {
        if (model.Groups.IsEmpty)
            return;

        var documentationFiles = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var assembly in documentation)
        {
            if (documentationFiles.ContainsKey(assembly.AssemblyName))
                continue;

            var members = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in assembly.Members)
                members.Add(member.Id, member.Xml);
            documentationFiles.Add(assembly.AssemblyName, members);
        }

        foreach (var group in model.Groups)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (group.InvalidLocation is not null)
            {
                if (!diagnosticsOnly)
                    continue;
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("ARKMF056", "Declare the MCP context as partial", "MCP context and containing types must be declared partial",
                        "Ark.Tools.MediatorFramework", DiagnosticSeverity.Error, true,
                        helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF056.md"),
                    ToLocation(group.InvalidLocation.Value)));
                continue;
            }

            var contracts = group.Contracts
                .Select(contract => CreateTool(contract, documentationFiles, context, diagnosticsOnly))
                .Where(static tool => tool is not null)
                .Select(static tool => tool!)
                .OrderBy(static tool => tool.Contract.Name, StringComparer.Ordinal)
                .ToImmutableArray();

            if (diagnosticsOnly)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var contract in contracts)
                {
                    if (!names.Add(contract.Contract.Name))
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateName, contract.Contract.Location, contract.Contract.Name));
                }

                continue;
            }

            var source = Render(group.Context, contracts);
            context.AddSource(group.Context.HintName + ".Mcp.g.cs", source);
        }
    }

    private static string GetHintName(INamedTypeSymbol type)
        => "Mcp_"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

    private static ImmutableArray<AssemblyContracts> FindReferencedContracts(
        Compilation compilation,
        ImmutableArray<string> assemblyNames,
        CancellationToken cancellationToken)
    {
        var toolAttributeType = compilation.GetTypeByMetadataName(ContractSymbols.McpToolAttribute);
        var builder = ImmutableArray.CreateBuilder<AssemblyContracts>(assemblyNames.Length);
        foreach (var assemblyName in assemblyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contracts = toolAttributeType is null
                ? ImmutableArray<ContractModel>.Empty
                : compilation.SourceModule.ReferencedAssemblySymbols
                    .Where(assembly => string.Equals(assembly.Name, assemblyName, StringComparison.Ordinal))
                    .SelectMany(assembly => AllTypes(assembly.GlobalNamespace, cancellationToken))
                    .Select(type => (Type: type, Attribute: type.GetAttributes().FirstOrDefault(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, toolAttributeType))))
                    .Where(static item => item.Attribute is not null)
                    .Select(static item => CreateContract(item.Type, item.Attribute!))
                    .ToImmutableArray();
            builder.Add(new AssemblyContracts(assemblyName, contracts));
        }

        return builder.MoveToImmutable();
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

    /// <summary>
    /// Reads everything a tool needs from the contract symbol. Validation diagnostics that do not depend
    /// on XML documentation are captured here; documentation is resolved at the output boundary.
    /// </summary>
    private static ContractModel CreateContract(INamedTypeSymbol type, AttributeData toolAttribute)
    {
        var kind = GetHandlerKind(type, out var responseType);
        var location = toolAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        var contractName = type.Name;
        var typeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var assemblyName = type.ContainingAssembly.Name;
        if (kind is null)
            return ContractModel.Invalid(typeFullName, assemblyName, location, new DiagnosticInfo(UnsupportedContract, location, contractName));

        var name = ContractSymbols.NamedString(toolAttribute, "Name") ?? contractName;
        var apiGroup = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass._isFullName(ContractSymbols.ApiGroupAttribute));
        var groupName = apiGroup?.ConstructorArguments.FirstOrDefault().Value as string;
        if (!string.IsNullOrWhiteSpace(groupName))
            name = groupName + "." + name;
        var version = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass._isFullName(ContractSymbols.VersioningAttribute));
        var introduced = ContractSymbols.NamedInt(version, "Introduced", 1);
        var retired = version is null ? 0 : ContractSymbols.NamedInt(version, "Retired");

        if (name.Length is 0 or > 128 || name.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
            return ContractModel.Invalid(typeFullName, assemblyName, location, new DiagnosticInfo(InvalidName, location, name));

        var properties = ContractProperties.All(type)
            .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
            .OrderBy(property => property.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray();
        var constructors = UsableConstructors(type, properties);
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var property in properties)
        {
            if (property.IsIndexer || !HasPublicSetter(type, property) && !constructors.Any(constructor => constructor.Parameters.Any(parameter =>
                string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase))))
                diagnostics.Add(new DiagnosticInfo(UnsupportedMember, property.Locations.FirstOrDefault(), contractName, property.Name));
        }
        if (diagnostics.Count > 0)
            return ContractModel.Invalid(typeFullName, assemblyName, location, diagnostics.ToArray());
        var constructor = FindConstructor(type, constructors, properties);
        if (constructor is null)
            return ContractModel.Invalid(typeFullName, assemblyName, location, new DiagnosticInfo(MissingConstructor, location, contractName));

        var httpEndpoint = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass._isFullName(ContractSymbols.HttpEndpointAttribute));
        var allowAnonymous = HasNamedArgument(toolAttribute, "AllowAnonymous")
            ? ContractSymbols.NamedBool(toolAttribute, "AllowAnonymous", false)
            : ContractSymbols.NamedBool(httpEndpoint, "AllowAnonymous", false);
        var hasDescription = TryGetDescription(type, out var attributeDescription);

        return new ContractModel(
            typeFullName,
            assemblyName,
            EquatableArray<DiagnosticInfo>.Empty,
            true,
            name,
            introduced,
            retired,
            ContractSymbols.NamedBool(toolAttribute, "ReadOnly", kind == HandlerKind.Query),
            ContractSymbols.NamedBool(toolAttribute, "Destructive", kind != HandlerKind.Query),
            ContractSymbols.NamedBool(toolAttribute, "Idempotent", false),
            ContractSymbols.NamedBool(toolAttribute, "OpenWorld", true),
            allowAnonymous,
            kind.Value,
            responseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            responseType is not null && IsAttachment(responseType),
            hasDescription,
            attributeDescription,
            CreateDocumentationSource(type),
            type.ContainingType is null ? null : CreateDocumentationSource(type.ContainingType),
            properties
                .Select(property => new PropertyModel(
                    property.Name,
                    ToInputType(property.Type),
                    IsAttachment(property.Type),
                    HasPublicSetter(type, property),
                    property.IsRequired,
                    TryGetDescription(property, out var propertyDescription),
                    propertyDescription,
                    CreateDocumentationSource(property)))
                .ToImmutableArray(),
            constructor.Parameters.Select(static parameter => parameter.Name).ToImmutableArray(),
            location)
        {
            IsSelfTyped = IsSelfTyped(type),
            ConstructorSetsRequiredMembers = constructor.GetAttributes().Any(attribute =>
                attribute.AttributeClass._isFullName("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute")),
        };
    }

    // A self-typed contract (IQuery<TSelf, TResult>, IRequest<TSelf, TResponse>, ICommand<TSelf>) is dispatched
    // through the typed processor overload, which resolves its handler without reflection and is trim-safe.
    private static bool IsSelfTyped(INamedTypeSymbol type)
        => type.AllInterfaces.Any(@interface =>
            (@interface.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + @interface.OriginalDefinition.MetadataName) is Query2 or Request2 or GenericCommand
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], type));

    private static DocumentationSource CreateDocumentationSource(ISymbol symbol)
        => new(
            symbol.GetDocumentationCommentXml() ?? string.Empty,
            symbol.ContainingAssembly.Name,
            symbol.GetDocumentationCommentId());

    private static ToolModel? CreateTool(
        ContractModel contract,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> documentationFiles,
        SourceProductionContext context,
        bool reportDiagnostics)
    {
        foreach (var diagnostic in reportDiagnostics ? contract.Diagnostics : EquatableArray<DiagnosticInfo>.Empty)
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.Arguments.Cast<object>().ToArray()));
        if (!contract.IsValid)
            return null;

        var summary = contract.HasDescriptionAttribute
            ? contract.AttributeDescription
            : XmlDocumentation(contract.Documentation, "summary", documentationFiles)
                ?? XmlDocumentation(contract.ContainingTypeDocumentation, "summary", documentationFiles);
        var remarks = contract.HasDescriptionAttribute
            ? null
            : XmlDocumentation(contract.Documentation, "remarks", documentationFiles)
                ?? XmlDocumentation(contract.ContainingTypeDocumentation, "remarks", documentationFiles);
        var description = summary is null
            ? remarks ?? string.Empty
            : remarks is null
                ? summary
                : summary + " " + remarks;
        if (description.Length == 0 && reportDiagnostics)
            context.ReportDiagnostic(Diagnostic.Create(MissingDescription, contract.Location, contract.Name));

        var propertyDescriptions = contract.Properties
            .Select(property => (property.Name, Description: property.HasDescriptionAttribute
                ? property.AttributeDescription
                : XmlDocumentation(property.Documentation, "summary", documentationFiles)))
            .Where(item => item.Description is not null)
            .ToImmutableDictionary(item => item.Name, item => item.Description!, StringComparer.Ordinal);

        return new ToolModel(contract, description.Length == 0 ? null : description, propertyDescriptions);
    }

    // Like Minimal API and Azure Functions, the generated tool only calls public setters and constructors.
    // An object initializer on the contract would bind a non-public member of the same name declared closer to
    // the contract, so a public property hidden that way is not settable.
    private static bool HasPublicSetter(INamedTypeSymbol type, IPropertySymbol property)
        => property.SetMethod is { DeclaredAccessibility: Accessibility.Public } && !IsHiddenByNonPublic(type, property);

    // The walk returns the most-derived public declaration, so any member of that name in a more-derived type is non-public.
    private static bool IsHiddenByNonPublic(INamedTypeSymbol type, IPropertySymbol property)
    {
        for (var current = type;
            current is not null && !SymbolEqualityComparer.Default.Equals(current, property.ContainingType);
            current = current.BaseType)
        {
            if (!current.GetMembers(property.Name).IsEmpty)
                return true;
        }

        return false;
    }

    // A constructor is usable when it is public and each parameter takes a contract property of the same type by value.
    private static ImmutableArray<IMethodSymbol> UsableConstructors(INamedTypeSymbol type, ImmutableArray<IPropertySymbol> properties)
        => type.Constructors
            .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.All(parameter =>
                parameter.RefKind == RefKind.None && properties.Any(property =>
                    string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                    && SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type))))
            .ToImmutableArray();

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

    // Per assembly, the first file by path wins; a malformed winner leaves the assembly undocumented.
    private static ImmutableArray<DocumentationAssembly> GetAdditionalDocumentation(
        ImmutableArray<DocumentationFileModel> additionalDocumentationFiles)
        => additionalDocumentationFiles
            .GroupBy(static file => file.AssemblyName, StringComparer.Ordinal)
            .Select(static files => files.OrderBy(static file => file.Path, StringComparer.Ordinal).First())
            .Where(static file => file.Members is not null)
            .OrderBy(static file => file.AssemblyName, StringComparer.Ordinal)
            .Select(static file => new DocumentationAssembly(file.AssemblyName, file.Members!.Value))
            .ToImmutableArray();

    private static ImmutableArray<DocumentationMember> GetDocumentationMembers(XDocument document, ImmutableArray<string> memberIds)
    {
        var members = ImmutableArray.CreateBuilder<DocumentationMember>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(memberIds, StringComparer.Ordinal);
        var membersElement = document.Root?.Element("members");
        if (membersElement is not null)
        {
            foreach (var member in membersElement.Elements("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (name is not null && wanted.Contains(name) && names.Add(name))
                    members.Add(new DocumentationMember(name, member.ToString(SaveOptions.DisableFormatting)));
            }
        }

        return members.ToImmutable();
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Replace('\\', '/');

    private static string? XmlDocumentation(
        DocumentationSource? source,
        string element,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> documentationFiles)
    {
        if (source is not { } documentation)
            return null;

        var xml = documentation.Xml;
        if (string.IsNullOrWhiteSpace(xml)
            && documentationFiles.TryGetValue(documentation.AssemblyName, out var documentationFile))
        {
            var documentationId = documentation.DocumentationId;
            if (documentationId is not null
                && documentationFile.TryGetValue(documentationId, out var member))
            {
                xml = member;
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
            candidate.AttributeClass._isFullName("System.ComponentModel.DescriptionAttribute"));
        description = attribute?.ConstructorArguments.FirstOrDefault().Value as string;
        return attribute is not null;
    }

    private static bool HasNamedArgument(AttributeData attribute, string name)
        => attribute.NamedArguments.Any(argument => argument.Key == name);

    private static string Render(ContextModel contextModel, ImmutableArray<ToolModel> contracts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("using global::Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        if (contextModel.Namespace is not null)
        {
            builder.Append("namespace ").Append(contextModel.Namespace).AppendLine(";");
            builder.AppendLine();
        }

        var indentation = 0;
        foreach (var containingType in contextModel.ContainingDeclarations)
        {
            builder.Append(' ', indentation * 4)
                .Append(containingType)
                .AppendLine();
            builder.Append(' ', indentation * 4).AppendLine("{");
            indentation++;
        }

        AppendIndented(builder, RenderContext(contextModel, contracts), indentation);
        while (indentation > 0)
        {
            indentation--;
            builder.Append(' ', indentation * 4).AppendLine("}");
        }

        return builder.ToString();
    }

    private static string RenderContext(ContextModel contextModel, ImmutableArray<ToolModel> contracts)
    {
        var builder = new StringBuilder();
        builder.Append(contextModel.Declaration)
            .Append(" : global::Ark.Tools.MediatorFramework.Mcp.IMcpToolContext")
            .Append(contextModel.Constraints)
            .AppendLine();
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder RegisterMcpTools(global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder builder)");
        builder.AppendLine("    {");
        RenderVersionMap(builder, contracts);
        // WithTools<T> takes JSON options only at registration time, so each tool resolves the shared options from DI.
        for (var index = 0; index < contracts.Length; index++)
        {
            builder.AppendLine("        builder.Services.AddSingleton<global::ModelContextProtocol.Server.McpServerTool>(static services => global::ModelContextProtocol.Server.McpServerTool.Create(");
            builder.Append("            typeof(Tool").Append(index).Append(").GetMethod(nameof(Tool").Append(index).AppendLine(".ExecuteAsync))!,");
            builder.AppendLine("            target: null,");
            builder.AppendLine("            new global::ModelContextProtocol.Server.McpServerToolCreateOptions");
            builder.AppendLine("            {");
            builder.AppendLine("                Services = services,");
            builder.AppendLine("                SerializerOptions = global::Ark.Tools.MediatorFramework.Mcp.McpToolSerializerOptions.Get(services),");
            builder.AppendLine("            }));");
        }

        builder.AppendLine("        return builder;");
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

    private static void RenderVersionMap(StringBuilder builder, ImmutableArray<ToolModel> contracts)
    {
        var maxVersion = contracts.Length == 0
            ? 1
            : contracts.Max(tool => Math.Max(tool.Contract.Introduced, tool.Contract.Retired));
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
                    .Where(tool => version >= tool.Contract.Introduced
                        && (tool.Contract.Retired == 0 || version < tool.Contract.Retired))
                    .Select(tool => "\"" + Escape(tool.Contract.Name) + "\"")));
            builder.AppendLine("],");
        }
        builder.AppendLine("                },");
        builder.Append("                [");
        builder.Append(string.Join(
            ", ",
            contracts
                .Where(tool => tool.Contract.Retired == 0)
                .Select(tool => "\"" + Escape(tool.Contract.Name) + "\"")));
        builder.AppendLine("]));");
    }

    private static void RenderTool(StringBuilder builder, ToolModel tool, int index)
    {
        var model = tool.Contract;
        var response = model.ResponseType;
        var attachmentResponse = model.AttachmentResponse;
        var returnType = attachmentResponse
            ? "global::System.Threading.Tasks.Task<global::ModelContextProtocol.Protocol.EmbeddedResourceBlock>"
            : model.Kind == HandlerKind.Command
                ? "global::System.Threading.Tasks.Task"
                : "global::System.Threading.Tasks.Task<" + response + ">";
        var parameters = model.Properties.Select(property =>
            "[global::System.ComponentModel.Description("
            + Literal(tool.PropertyDescriptions.TryGetValue(property.Name, out var propertyDescription)
                ? propertyDescription
                : string.Empty) + ")] "
            + property.InputType + " " + ToParameterName(property.Name));
        // The parameters keep the property names, which are the tool's input schema, so the adapter's own identifiers give way.
        var taken = new HashSet<string>(model.Properties.Select(static property => ToParameterName(property.Name)), StringComparer.Ordinal);
        var services = FreeName("services");
        var cancellationToken = FreeName("cancellationToken");
        var request = FreeName("request");
        var result = FreeName("result");

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
        if (tool.Description is not null)
            builder.Append("        [global::System.ComponentModel.Description(").Append(Literal(tool.Description)).AppendLine(")]");
        builder.Append("        [global::Microsoft.AspNetCore.Authorization.")
            .Append(model.AllowAnonymous ? "AllowAnonymousAttribute" : "AuthorizeAttribute")
            .AppendLine("]");
        builder.Append("        public static async ").Append(returnType).Append(" ExecuteAsync(")
            .Append(string.Join(", ", parameters)).Append(model.Properties.Count > 0 ? ", " : string.Empty)
            .Append("global::System.IServiceProvider ").Append(services).Append(", global::System.Threading.CancellationToken ").Append(cancellationToken).Append(')').AppendLine();
        builder.AppendLine("        {");
        builder.Append("            var ").Append(request).Append(" = new ").Append(model.TypeFullName).Append("(");
        builder.Append(string.Join(", ", model.ConstructorParameters.Select(parameter =>
            ToParameterName(model.Properties.First(property => string.Equals(property.Name, parameter, StringComparison.OrdinalIgnoreCase)).Name)
            + (model.Properties.First(property => string.Equals(property.Name, parameter, StringComparison.OrdinalIgnoreCase)).IsAttachment
                ? ".ToAttachment()" : string.Empty))));
        var settable = model.Properties.Where(property => property.HasSetter).ToImmutableArray();
        if (settable.Length > 0)
        {
            builder.AppendLine(")");
            builder.AppendLine("            {");
            foreach (var property in settable)
                // Without [SetsRequiredMembers] on the constructor, C# requires the initializer for every required member.
                if (property.IsRequired && !model.ConstructorSetsRequiredMembers
                    || !model.ConstructorParameters.Any(parameter => string.Equals(parameter, property.Name, StringComparison.OrdinalIgnoreCase)))
                    builder.Append("                ").Append(property.Name).Append(" = ").Append(ToInputValue(property)).AppendLine(",");
            builder.AppendLine("            };");
        }
        else
            builder.AppendLine(");");
        if (model.Kind == HandlerKind.Query)
        {
            builder.Append("            var ").Append(result).Append(" = await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IQueryProcessor>(").Append(services).Append(").ExecuteAsync<")
                .Append(TypedDispatchArguments(model)).Append(">(").Append(request).Append(", ").Append(cancellationToken).Append(").ConfigureAwait(false);").AppendLine();
            if (attachmentResponse)
                builder.Append("            return await global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentResults.ToEmbeddedResourceAsync(").Append(result)
                    .Append(", cancellationToken: ").Append(cancellationToken).AppendLine(").ConfigureAwait(false);");
            else
                builder.Append("            return ").Append(result).AppendLine(";");
        }
        else if (model.Kind == HandlerKind.Request)
        {
            builder.Append("            var ").Append(result).Append(" = await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.IRequestProcessor>(").Append(services).Append(").ExecuteAsync<")
                .Append(TypedDispatchArguments(model)).Append(">(").Append(request).Append(", ").Append(cancellationToken).Append(").ConfigureAwait(false);").AppendLine();
            if (attachmentResponse)
                builder.Append("            return await global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentResults.ToEmbeddedResourceAsync(").Append(result)
                    .Append(", cancellationToken: ").Append(cancellationToken).AppendLine(").ConfigureAwait(false);");
            else
                builder.Append("            return ").Append(result).AppendLine(";");
        }
        else
        {
            builder.Append("            await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Ark.Tools.Solid.ICommandProcessor>(").Append(services).Append(").ExecuteAsync")
                .Append(model.IsSelfTyped ? "<" + model.TypeFullName + ">" : string.Empty).Append('(').Append(request).Append(", ").Append(cancellationToken).AppendLine(").ConfigureAwait(false);");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        string FreeName(string name)
        {
            while (!taken.Add(name))
                name = "_" + name;
            return name;
        }
    }

    private static string TypedDispatchArguments(ContractModel model)
        => model.IsSelfTyped ? model.TypeFullName + ", " + model.ResponseType : model.ResponseType!;

    private static string ToParameterName(string name)
    {
        var parameterName = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return SyntaxFacts.GetKeywordKind(parameterName) == SyntaxKind.None ? parameterName : "@" + parameterName;
    }

    private static string ToInputType(ITypeSymbol type)
        => IsAttachment(type)
            ? "global::Ark.Tools.MediatorFramework.Mcp.McpAttachmentInput"
            : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string ToInputValue(PropertyModel property)
        => property.IsAttachment
            ? ToParameterName(property.Name) + ".ToAttachment()"
            : ToParameterName(property.Name);

    private static IMethodSymbol? FindConstructor(INamedTypeSymbol type, ImmutableArray<IMethodSymbol> constructors, ImmutableArray<IPropertySymbol> properties)
        => constructors
            .Where(candidate => properties.Where(property => !HasPublicSetter(type, property)).All(property =>
                candidate.Parameters.Any(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(candidate => candidate.Parameters.Length)
            .FirstOrDefault();

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Literal(string value)
        => SyntaxFactory.Literal(value).ToFullString();

    private readonly record struct DocumentationFileModel(string Path, string AssemblyName, EquatableArray<DocumentationMember>? Members);
    private sealed record DocumentationRequest(EquatableArray<string> AssemblyNames, EquatableArray<string> MemberIds);
    private readonly record struct DocumentationMember(string Id, string Xml);
    private sealed record DocumentationAssembly(string AssemblyName, EquatableArray<DocumentationMember> Members);
    private readonly record struct DocumentationSource(string Xml, string AssemblyName, string? DocumentationId);
    private sealed record MarkerModel(string ContextMetadataName, string? AssemblyName, MarkerLocation? InvalidLocation, ContextModel Context);
    private sealed record ContextModel(
        string? Namespace,
        EquatableArray<string> ContainingDeclarations,
        string Declaration,
        string Constraints,
        string HintName);
    private readonly record struct MarkerLocation(
        string FilePath,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter);
    private sealed record ContextGroup(ContextModel Context, MarkerLocation? InvalidLocation, EquatableArray<ContractModel> Contracts)
    {
        // Emission only needs to know the context is invalid, not where.
        public ContextGroup ForEmission() => this with
        {
            InvalidLocation = InvalidLocation is null ? null : default(MarkerLocation),
            Contracts = Contracts.Values.Select(static contract => contract.ForEmission()).ToImmutableArray(),
        };
    }
    private sealed record McpModel(EquatableArray<ContextGroup> Groups, EquatableArray<string> DocumentationAssemblyNames)
    {
        // The emitted source reads neither locations nor diagnostics, so dropping them keeps it cached when lines shift.
        public McpModel ForEmission() => this with { Groups = Groups.Values.Select(static group => group.ForEmission()).ToImmutableArray() };
    }
    private sealed record AssemblyContracts(string AssemblyName, EquatableArray<ContractModel> Contracts);

    // Roslyn source locations compare by syntax tree and span, so edits in other files keep models equal while
    // reported diagnostics keep honoring #pragma and per-file EditorConfig suppressions.
    // Any edit in the declaring file reruns the diagnostics output; ForEmission drops them from the emission input.
    private readonly record struct DiagnosticInfo
    {
        public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
        {
            Descriptor = descriptor;
            Location = location;
            Arguments = arguments.ToImmutableArray();
        }

        public DiagnosticDescriptor Descriptor { get; }
        public Location? Location { get; }
        public EquatableArray<string> Arguments { get; }
    }

    private readonly record struct PropertyModel(
        string Name,
        string InputType,
        bool IsAttachment,
        bool HasSetter,
        bool IsRequired,
        bool HasDescriptionAttribute,
        string? AttributeDescription,
        DocumentationSource Documentation);

    private sealed record ContractModel(
        string TypeFullName,
        string AssemblyName,
        EquatableArray<DiagnosticInfo> Diagnostics,
        bool IsValid,
        string Name,
        int Introduced,
        int Retired,
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld,
        bool AllowAnonymous,
        HandlerKind Kind,
        string? ResponseType,
        bool AttachmentResponse,
        bool HasDescriptionAttribute,
        string? AttributeDescription,
        DocumentationSource Documentation,
        DocumentationSource? ContainingTypeDocumentation,
        EquatableArray<PropertyModel> Properties,
        EquatableArray<string> ConstructorParameters,
        Location? Location)
    {
        public bool IsSelfTyped { get; init; }

        public bool ConstructorSetsRequiredMembers { get; init; }

        public ContractModel ForEmission() => this with { Location = null, Diagnostics = EquatableArray<DiagnosticInfo>.Empty };

        public static ContractModel Invalid(
            string typeFullName,
            string assemblyName,
            Location? location,
            params DiagnosticInfo[] diagnostics)
            => new(
                typeFullName,
                assemblyName,
                diagnostics.ToImmutableArray(),
                false,
                string.Empty,
                0,
                0,
                false,
                false,
                false,
                false,
                false,
                default,
                null,
                false,
                false,
                null,
                default,
                null,
                EquatableArray<PropertyModel>.Empty,
                EquatableArray<string>.Empty,
                location);
    }

    private sealed record ToolModel(
        ContractModel Contract,
        string? Description,
        ImmutableDictionary<string, string> PropertyDescriptions);

    private enum HandlerKind { Query, Request, Command }

}
