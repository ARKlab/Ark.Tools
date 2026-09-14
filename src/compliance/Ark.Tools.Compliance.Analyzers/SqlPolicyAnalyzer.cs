// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Immutable;
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
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true);

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
            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                    "build_property.EnableArkToolsCompliance", out var enabled)
                && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            start.RegisterSymbolAction(_analyzeType, SymbolKind.NamedType);
        });
    }

    private static void _analyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (_hasAttribute(type, "Ark.Tools.Compliance.Sql.SqlDataPolicyAttribute"))
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers().Where(static m => !m.IsStatic && !m.IsImplicitlyDeclared
                             && m.Kind is SymbolKind.Property or SymbolKind.Field))
                {
                    var valueType = ComplianceSymbolFacts._getValueType(member);
                    if ((ComplianceSymbolFacts._isClassified(member)
                         || ComplianceSymbolFacts._isClassified(type)
                         || (valueType is not null && ComplianceSymbolFacts._isClassified(ComplianceSymbolFacts._unwrapNullable(valueType))))
                        && !_hasAttribute(member, "Ark.Tools.Compliance.Sql.SqlColumnPolicyAttribute"))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(_missingColumn,
                            member.Locations.FirstOrDefault(static l => l.IsInSource) ?? type.Locations.FirstOrDefault(),
                            member.ToDisplayString()));
                    }
                }
            }
        }

    }

    private static bool _hasAttribute(ISymbol symbol, string name)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);
    }
}
