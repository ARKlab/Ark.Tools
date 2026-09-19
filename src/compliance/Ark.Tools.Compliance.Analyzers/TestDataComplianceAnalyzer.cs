// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII006.md");
    private static readonly DiagnosticDescriptor _scanIncomplete = new(
        "ARKPII014", "Complete the test-data compliance scan",
        "Test data could not be fully scanned for personal data because a pattern exceeded the analyzer time limit. Shorten or replace the literal.",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII014.md");
    private static readonly ImmutableArray<Pattern> _defaultPatterns = ImmutableArray.Create(
        new Pattern(PersonalDataKind.Email, PersonalDataPatterns._email),
        new Pattern(PersonalDataKind.Phone, PersonalDataPatterns._phone),
        new Pattern(PersonalDataKind.NationalIdentifier, PersonalDataPatterns._nationalIdentifier),
        new Pattern(PersonalDataKind.Iban, PersonalDataPatterns._iban),
        new Pattern(PersonalDataKind.PostalAddress, PersonalDataPatterns._postalAddress));
    private readonly ImmutableArray<Pattern> _patterns;

    /// <summary>Initializes a new instance of the <see cref="TestDataComplianceAnalyzer"/> class.</summary>
    public TestDataComplianceAnalyzer()
        : this(_defaultPatterns)
    {
    }

    internal TestDataComplianceAnalyzer(Regex emailPattern)
        : this(_defaultPatterns.SetItem(0, new Pattern(PersonalDataKind.Email, emailPattern)))
    {
    }

    private TestDataComplianceAnalyzer(ImmutableArray<Pattern> patterns)
    {
        _patterns = patterns;
    }

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule, _scanIncomplete);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                    "build_property.EnableArkToolsCompliance", out var enabled)
                && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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

                var result = _find(value, _patterns, operationContext.CancellationToken);
                if (result._findings.Count == 0 && !result._timedOut)
                {
                    return;
                }

                if (result._findings.Count > 0)
                {
                    var replacement = new StringBuilder(value);
                    foreach (var match in result._findings.OrderByDescending(static match => match._span.Start))
                    {
                        operationContext.CancellationToken.ThrowIfCancellationRequested();
                        replacement.Remove(match._span.Start, match._span.Length)
                            .Insert(match._span.Start, PersonalDataPatterns._reservedValue(match._kind));
                    }

                    operationContext.ReportDiagnostic(Diagnostic.Create(_rule, literal.Syntax.GetLocation(),
                        ImmutableDictionary<string, string?>.Empty.Add("Replacement", replacement.ToString()),
                        result._findings[0]._kind.ToString()));
                }

                if (result._timedOut)
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(_scanIncomplete, literal.Syntax.GetLocation()));
                }
            }, OperationKind.Literal);
        });
        context.RegisterAdditionalFileAction(fileContext =>
        {
            if (fileContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                    "build_property.EnableArkToolsCompliance", out var enabled)
                && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_isTestProject(fileContext.Compilation, fileContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions)
                && !_isTestPath(fileContext.AdditionalFile.Path))
            {
                return;
            }

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

                var result = _find(content, _patterns, fileContext.CancellationToken);
                foreach (var match in result._findings)
                {
                    fileContext.CancellationToken.ThrowIfCancellationRequested();
                    var span = new TextSpan(line.Start + match._span.Start, match._span.Length);
                    fileContext.ReportDiagnostic(Diagnostic.Create(_rule,
                        Location.Create(file.Path, span, text.Lines.GetLinePositionSpan(span)),
                        ImmutableDictionary<string, string?>.Empty.Add("Replacement",
                            PersonalDataPatterns._reservedValue(match._kind)),
                        match._kind.ToString()));
                }

                if (result._timedOut)
                {
                    var lineSpan = new TextSpan(line.Start, line.Span.Length);
                    fileContext.ReportDiagnostic(Diagnostic.Create(_scanIncomplete,
                        Location.Create(file.Path, lineSpan, text.Lines.GetLinePositionSpan(lineSpan))));
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
        if (name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
            || name is "Tests" or "Test")
        {
            return true;
        }

        foreach (var assembly in compilation.ReferencedAssemblyNames)
        {
            if (assembly.Name is "Microsoft.VisualStudio.TestPlatform.TestFramework" or "xunit.core" or "nunit.framework")
            {
                return true;
            }
        }

        return false;
    }

    private static bool _isTestPath(string path)
    {
        var normalized = "/" + path.Replace('\\', '/');
        return normalized.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("/test/", StringComparison.OrdinalIgnoreCase) >= 0
            || Path.GetFileName(path).EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static ScanResult _find(
        string value,
        ImmutableArray<Pattern> patterns,
        CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();
        var timedOut = false;
        foreach (var pattern in patterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (Match match in pattern._regex.Matches(value))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (PersonalDataPatterns._isReserved(pattern._kind, match.Value)
                        || !PersonalDataPatterns._isChecksumValid(pattern._kind, match.Value))
                    {
                        continue;
                    }

                    var span = new TextSpan(match.Index, match.Length);
                    var overlaps = false;
                    foreach (var finding in findings)
                    {
                        if (finding._span.OverlapsWith(span))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        findings.Add(new Finding(pattern._kind, span));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                timedOut = true;
            }
        }

        return new ScanResult(findings, timedOut);
    }

    private sealed class Pattern
    {
        internal Pattern(PersonalDataKind kind, string pattern)
        {
            _kind = kind;
            _regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }

        internal Pattern(PersonalDataKind kind, Regex regex)
        {
            _kind = kind;
            _regex = regex;
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

    private readonly struct ScanResult
    {
        internal ScanResult(List<Finding> findings, bool timedOut)
        {
            _findings = findings;
            _timedOut = timedOut;
        }

        internal List<Finding> _findings { get; }
        internal bool _timedOut { get; }
    }
}
