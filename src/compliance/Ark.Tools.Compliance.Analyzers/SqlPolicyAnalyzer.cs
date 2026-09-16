// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Requires explicit SQL column policies for classified members.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlPolicyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _missingColumn = new(
        "ARKPII007", "Declare storage protection for a classified column",
        "Classified member '{0}' has no SqlColumnPolicy on a SqlDataPolicy type; declare its verbatim column name and storage protection",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII007.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_missingColumn);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var options = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (!ComplianceCompilationFacts._isEnabled(options))
            {
                return;
            }

            var facts = ComplianceCompilationFacts._create(
                start.Compilation,
                options);

            start.RegisterSymbolAction(symbolContext => _analyzeType(symbolContext, facts), SymbolKind.NamedType);
        });
    }

    private static void _analyzeType(SymbolAnalysisContext context, ComplianceCompilationFacts facts)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (ComplianceSymbolFacts._hasAttribute(type, facts._sqlDataPolicyAttribute))
        {
            var classifiedCache = new Dictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
            bool isClassified(ISymbol? candidate)
            {
                if (candidate is null)
                {
                    return false;
                }

                if (!classifiedCache.TryGetValue(candidate, out var result))
                {
                    result = ComplianceSymbolFacts._isClassified(candidate, facts);
                    classifiedCache[candidate] = result;
                }

                return result;
            }

            var typeIsClassified = isClassified(type);
            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers().Where(static m => !m.IsStatic && !m.IsImplicitlyDeclared
                             && m.Kind is SymbolKind.Property or SymbolKind.Field))
                {
                    var valueType = ComplianceSymbolFacts._getValueType(member);
                    var unwrappedValueType = valueType is null ? null : ComplianceSymbolFacts._unwrapNullable(valueType);
                    if ((isClassified(member)
                         || typeIsClassified
                         || isClassified(unwrappedValueType))
                        && !ComplianceSymbolFacts._hasAttribute(member, facts._sqlColumnPolicyAttribute))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(_missingColumn,
                            member.Locations.FirstOrDefault(static l => l.IsInSource) ?? type.Locations.FirstOrDefault(),
                            member.ToDisplayString()));
                    }
                }
            }
        }

    }
}
