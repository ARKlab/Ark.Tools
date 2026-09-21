// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.IO;
using System.Linq;

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

    private static readonly DiagnosticDescriptor _drift = new(
        "ARKPII020", "Compliance surface changed",
        "Compliance surface differs from ArkComplianceSurface.txt: {0}. Review obj/.../ArkComplianceSurface.current.txt and copy it to ArkComplianceSurface.txt to accept this change.",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII020.md");

    private static readonly DiagnosticDescriptor _weakened = new(
        "ARKPII021", "Classified member removed or weakened",
        "Classified member '{0}' is absent from the baseline or its classification has been weakened. Review this privacy change before updating ArkComplianceSurface.txt",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII021.md");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var members = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax,
                static (syntaxContext, token) => _parseType(syntaxContext, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!)
            .WithTrackingName(MemberSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<TypeSpec>(specs));
        var reveals = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => _isRevealCandidate(node),
                static (syntaxContext, token) => _parseReveal(syntaxContext, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!.Value)
            .WithTrackingName(RevealSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<RevealSpec>(specs));
        var registrations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => _isRegistrationCandidate(node),
                static (syntaxContext, token) => _parseRegistration(syntaxContext, token))
            .Where(static spec => spec is not null)
            .Select(static (spec, _) => spec!)
            .WithTrackingName(RegistrationSpecsTrackingName)
            .Collect()
            .Select(static (specs, _) => new ImmutableEquatableArray<RegistrationSpec>(specs));
        var inventory = members.Combine(reveals).Combine(registrations)
            .Select(static (input, token) => _build(input.Left.Left, input.Left.Right, input.Right, token))
            .WithTrackingName(SurfaceTrackingName);
        var baselines = context.AdditionalTextsProvider
            .Where(static file => string.Equals(
                Path.GetFileName(file.Path),
                "ArkComplianceSurface.txt",
                StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => file.GetText(token)?.ToString())
            .Collect()
            .Select(static (files, _) => new ImmutableEquatableArray<string?>(files));
        var enabled = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            !(options.GlobalOptions.TryGetValue("build_property.EnableArkToolsCompliance", out var compliance)
                && string.Equals(compliance, "false", StringComparison.OrdinalIgnoreCase))
            && options.GlobalOptions.TryGetValue("build_property.ArkComplianceSurfaceEnabled", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        context.RegisterSourceOutput(inventory, static (production, surface) =>
            production.AddSource("ArkComplianceSurface.g.cs", "/*\n" + surface.Text + "*/\n"));
        context.RegisterSourceOutput(inventory.Combine(baselines).Combine(enabled), static (production, input) =>
        {
            var ((surface, files), isEnabled) = input;
            if (isEnabled)
                _verify(production, surface, files);
        });
    }
}
