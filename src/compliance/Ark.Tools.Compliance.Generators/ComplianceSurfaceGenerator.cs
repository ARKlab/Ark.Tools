// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.Compliance.Generators;

/// <summary>Inventories classified members and gates changes against a reviewed compliance baseline.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ComplianceSurfaceGenerator : IIncrementalGenerator
{
    private const string Header = "COMPLIANCE-SURFACE 1";
    private const string Prefix = "Ark.Tools.Compliance.";
    private const string MemberSpecsTrackingName = "ComplianceSurfaceMemberSpecs";
    private const string RevealSpecsTrackingName = "ComplianceSurfaceRevealSpecs";
    private const string RegistrationSpecsTrackingName = "ComplianceSurfaceRegistrationSpecs";
    private const string SurfaceTrackingName = "ComplianceSurfaceModel";
    private const string SurfaceTextTrackingName = "ComplianceSurfaceText";

    private static readonly DiagnosticDescriptor _drift = new(
        "ARKPII020", "Compliance surface changed",
        "Compliance surface differs from ArkComplianceSurface.txt: {0}. Review obj/.../ArkComplianceSurface.current.txt and copy it to ArkComplianceSurface.txt to accept this change.",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII020.md");

    private static readonly DiagnosticDescriptor _weakened = new(
        "ARKPII021", "Classified member removed or weakened",
        "Classified member '{0}' is absent from the baseline or its classification has been weakened. Review this privacy change before updating ArkComplianceSurface.txt.",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII021.md");

    // Generators get a new SemanticModel per GetSemanticModel call, and each one rebinds the whole enclosing method
    // body: calls in one method share one model per tree so binding them is not quadratic.
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, SemanticModel>> _semanticModels = new();

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enabled = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            !(options.GlobalOptions.TryGetValue("build_property.EnableArkToolsCompliance", out var compliance)
                && string.Equals(compliance, "false", StringComparison.OrdinalIgnoreCase))
            && options.GlobalOptions.TryGetValue("build_property.ArkComplianceSurfaceEnabled", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var baselines = context.AdditionalTextsProvider
            .Where(static file => string.Equals(
                Path.GetFileName(file.Path),
                "ArkComplianceSurface.txt",
                StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => file.GetText(token)?.ToString())
            .Collect()
            .Select(static (files, _) => new ImmutableEquatableArray<string?>(files));
        // Unclassified members matter only to tell a weakened baseline entry from a removed one, so they are kept
        // only for types the baseline names.
        var baselineTypes = baselines.Select(static (files, token) => _baselineTypes(files, token));
        var members = _whenEnabled(context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax,
                static (syntaxContext, _) => syntaxContext.Node), enabled, context.CompilationProvider)
            .Combine(baselineTypes)
            .Select(static (input, token) => _parseType(
                input.Left.Compilation.GetSemanticModel(input.Left.Node.SyntaxTree), input.Left.Node, input.Right, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!)
            .WithTrackingName(MemberSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<TypeSpec>(specs));
        var reveals = _whenEnabled(context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => _isRevealCandidate(node),
                static (syntaxContext, _) => syntaxContext.Node), enabled, context.CompilationProvider)
            .Select(static (candidate, token) => _parseReveal(
                _semanticModel(candidate.Compilation, candidate.Node.SyntaxTree), candidate.Node, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!.Value)
            .WithTrackingName(RevealSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<RevealSpec>(specs));
        var registrations = _whenEnabled(context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => _isRegistrationCandidate(node),
                static (syntaxContext, _) => syntaxContext.Node), enabled, context.CompilationProvider)
            .Select(static (candidate, token) => _parseRegistration(
                _semanticModel(candidate.Compilation, candidate.Node.SyntaxTree), candidate.Node, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!)
            .WithTrackingName(RegistrationSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<RegistrationSpec>(specs));
        var inventory = members.Combine(reveals).Combine(registrations)
            .Select(static (input, token) => _build(input.Left.Left, input.Left.Right, input.Right, token))
            .WithTrackingName(SurfaceTrackingName);

        // Only the text is emitted, so an edit that shifts member locations does not re-add identical source.
        var text = inventory
            .Select(static (surface, _) => surface.Text)
            .WithTrackingName(SurfaceTextTrackingName);
        context.RegisterSourceOutput(text.Combine(enabled), static (production, input) =>
        {
            if (input.Right)
                production.AddSource("ArkComplianceSurface.g.cs", "/*\n" + input.Left + "*/\n");
        });
        context.RegisterSourceOutput(inventory.Combine(baselines).Combine(enabled), static (production, input) =>
        {
            var ((surface, files), isEnabled) = input;
            if (isEnabled)
                _verify(production, surface, files);
        });
    }

    // The semantic parse runs only after the gate: a disabled surface must not bind every type on each edit. Only
    // syntax nodes cross the gate, so a disabled surface keeps no semantic model or compilation in its tables.
    private static IncrementalValuesProvider<(SyntaxNode Node, Compilation Compilation)> _whenEnabled(
        IncrementalValuesProvider<SyntaxNode> candidates,
        IncrementalValueProvider<bool> enabled,
        IncrementalValueProvider<Compilation> compilation)
    {
        return candidates.Combine(enabled)
            .Where(static candidate => candidate.Right)
            .Select(static (candidate, _) => candidate.Left)
            .Combine(compilation)
            .Select(static (candidate, _) => (candidate.Left, candidate.Right));
    }

    private static SemanticModel _semanticModel(Compilation compilation, SyntaxTree tree)
    {
        return _semanticModels.GetValue(compilation, static _ => new ConcurrentDictionary<SyntaxTree, SemanticModel>())
            .GetOrAdd(tree, static (key, owner) => owner.GetSemanticModel(key), compilation);
    }
}
