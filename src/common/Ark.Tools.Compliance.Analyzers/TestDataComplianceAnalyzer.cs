// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Ark.Tools.Compliance.Internal;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Finds plausible personal-data literals in test source and Reqnroll table cells.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestDataComplianceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _rule = new(
        "ARKPII006", "Keep real personal data out of test fixtures",
        "Test data contains a plausible {0}; repository clones and CI logs retain it. Replace it with reserved test data.",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    private static readonly ImmutableArray<Pattern> _patterns = ImmutableArray.Create(
        new Pattern(PersonalDataKind.Email, PersonalDataPatterns._email),
        new Pattern(PersonalDataKind.Phone, PersonalDataPatterns._phone),
        new Pattern(PersonalDataKind.NationalIdentifier, PersonalDataPatterns._nationalIdentifier),
        new Pattern(PersonalDataKind.Iban, PersonalDataPatterns._iban),
        new Pattern(PersonalDataKind.PostalAddress, PersonalDataPatterns._postalAddress));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var isTestProject = _isTestProject(start.Compilation, start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            start.RegisterOperationAction(operationContext =>
            {
                if (!isTestProject && !_isTestPath(operationContext.Operation.Syntax.SyntaxTree.FilePath))
                {
                    return;
                }

                if (operationContext.Operation is not ILiteralOperation { ConstantValue: { HasValue: true, Value: string value } } literal)
                {
                    return;
                }

                var matches = _find(value);
                if (matches.Count == 0)
                {
                    return;
                }

                var replacement = new StringBuilder(value);
                foreach (var match in matches.OrderByDescending(static match => match._span.Start))
                {
                    replacement.Remove(match._span.Start, match._span.Length)
                        .Insert(match._span.Start, PersonalDataPatterns._reservedValue(match._kind));
                }

                operationContext.ReportDiagnostic(Diagnostic.Create(_rule, literal.Syntax.GetLocation(),
                    ImmutableDictionary<string, string?>.Empty.Add("Replacement", replacement.ToString()),
                    matches[0]._kind.ToString()));
            }, OperationKind.Literal);
        });
        context.RegisterAdditionalFileAction(static fileContext =>
        {
            var file = fileContext.AdditionalFile;
            if (!file.Path.EndsWith(".feature", StringComparison.OrdinalIgnoreCase)
                || file.GetText(fileContext.CancellationToken) is not { } text)
            {
                return;
            }

            foreach (var line in text.Lines)
            {
                fileContext.CancellationToken.ThrowIfCancellationRequested();
                var content = line.ToString();
                if (!content.TrimStart().StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var match in _find(content))
                {
                    var span = new TextSpan(line.Start + match._span.Start, match._span.Length);
                    fileContext.ReportDiagnostic(Diagnostic.Create(_rule,
                        Location.Create(file.Path, span, text.Lines.GetLinePositionSpan(span)),
                        ImmutableDictionary<string, string?>.Empty.Add("Replacement",
                            PersonalDataPatterns._reservedValue(match._kind)),
                        match._kind.ToString()));
                }
            }
        });
    }

    private static bool _isTestProject(Compilation compilation, AnalyzerConfigOptions options)
    {
        if (options.TryGetValue("build_property.IsTestProject", out var value))
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        var name = compilation.AssemblyName ?? string.Empty;
        return name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
            || name is "Tests" or "Test"
            || compilation.ReferencedAssemblyNames.Any(static assembly =>
                assembly.Name is "Microsoft.VisualStudio.TestPlatform.TestFramework" or "xunit.core" or "nunit.framework");
    }

    private static bool _isTestPath(string path)
    {
        var normalized = "/" + path.Replace('\\', '/');
        return normalized.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("/test/", StringComparison.OrdinalIgnoreCase) >= 0
            || Path.GetFileName(path).EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static List<Finding> _find(string value)
    {
        var findings = new List<Finding>();
        foreach (var pattern in _patterns)
        {
            foreach (Match match in pattern._regex.Matches(value))
            {
                if (PersonalDataPatterns._isReserved(pattern._kind, match.Value)
                    || !PersonalDataPatterns._isChecksumValid(pattern._kind, match.Value))
                {
                    continue;
                }

                var span = new TextSpan(match.Index, match.Length);
                if (!findings.Any(finding => finding._span.OverlapsWith(span)))
                {
                    findings.Add(new Finding(pattern._kind, span));
                }
            }
        }

        return findings;
    }

    private sealed class Pattern
    {
        internal Pattern(PersonalDataKind kind, string pattern)
        {
            _kind = kind;
            _regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }

        internal PersonalDataKind _kind { get; }
        internal Regex _regex { get; }
    }

    private sealed class Finding
    {
        internal Finding(PersonalDataKind kind, TextSpan span)
        {
            _kind = kind;
            _span = span;
        }

        internal PersonalDataKind _kind { get; }
        internal TextSpan _span { get; }
    }
}
