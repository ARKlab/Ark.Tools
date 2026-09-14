// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Analyzers.Tests;

/// <summary>
/// End-user expectation tests: the high-value promise of the library is that merely *tagging* a
/// member with a data classification attribute - without adopting the sensitive value object
/// structs - is enough for the analyzer to reject that member reaching an NLog logger.
///
/// The scenarios mirror exactly what an application developer writes:
/// <code>
/// record MyData([PersonalData] string? EMail);
/// var o = new MyData("you@me.com"); // o arrives from transport or persistence
/// _logger.Info("Email is:{email}", o.EMail);   // ARKPII002
/// _logger.Info("Object is:{@object}", o);      // ARKPII002
/// var safe = o.EMail;
/// _logger.Info("Email is:{email}", safe);      // ARKPII002
/// </code>
/// </summary>
[TestClass]
public sealed class TaggedOnlyNLogExpectationTests
{
    private static readonly ImmutableArray<MetadataReference> _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    // The minimal surface an end-user project sees: the Ark classification attribute (recognized
    // through its Microsoft.Extensions.Compliance base) and NLog's Logger (a default log sink,
    // recognized by its type name - no analyzer configuration required).
    private const string _support = """
        using System;
        namespace Microsoft.Extensions.Compliance.Classification
        {
            public abstract class DataClassificationAttribute : Attribute { }
        }
        namespace Ark.Tools.Compliance
        {
            [AttributeUsage(AttributeTargets.All)]
            public sealed class PersonalDataAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
            [AttributeUsage(AttributeTargets.All)]
            public sealed class SecretAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
        }
        namespace NLog
        {
            public class Logger
            {
                public void Info(string message, params object[] args) { }
            }
        }
        """;

    /// <summary>
    /// Tagged-only PII on a positional record parameter: every way of routing it into an NLog call
    /// - the tagged member directly, the whole object via {@object} destructuring, or a copy
    /// through a local variable - is rejected with ARKPII002.
    /// </summary>
    [TestMethod]
    [DataRow("""_logger.Info("Email is:{email}", o.EMail);""")]
    [DataRow("""_logger.Info("Object is:{@object}", o);""")]
    [DataRow("""var safe = o.EMail; _logger.Info("Email is:{email}", safe);""")]
    public async Task TaggedRecordMember_LoggedWithNLog_ReportsError(string statement)
    {
        var diagnostics = await _analyzeAsync(_userCode(statement)).ConfigureAwait(false);

        diagnostics.Should().Contain(static diagnostic =>
            diagnostic.Id == "ARKPII002" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>A tagged field and a tagged property on a plain class are equally protected.</summary>
    [TestMethod]
    [DataRow("""_logger.Info("Token is:{token}", d.Token);""")]
    [DataRow("""_logger.Info("Phone is:{phone}", d.Phone);""")]
    [DataRow("""_logger.Info("Object is:{@data}", d);""")]
    public async Task TaggedClassMembers_LoggedWithNLog_ReportsError(string statement)
    {
        var diagnostics = await _analyzeAsync(_userCode(statement)).ConfigureAwait(false);

        diagnostics.Should().Contain(static diagnostic =>
            diagnostic.Id == "ARKPII002" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>Logging the untagged key of the same record stays clean: log the key, not the person.</summary>
    [TestMethod]
    public async Task UntaggedKey_LoggedWithNLog_IsAccepted()
    {
        var diagnostics = await _analyzeAsync(_userCode("""_logger.Info("Id is:{id}", o.Id);""")).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    private static string _userCode(string statement)
    {
        return """
            using Ark.Tools.Compliance;

            record MyData(int Id, [PersonalData] string? EMail);

            class LegacyData
            {
                [Secret] public string? Token;
                [PersonalData] public string? Phone { get; set; }
            }

            class MyService
            {
                private readonly NLog.Logger _logger = new NLog.Logger();

                void Process(MyData o, LegacyData d) // o arrives from transport or persistence
                {
            """ + statement + """
                }
            }
            """;
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create("TaggedOnlyNLogTests",
            [CSharpSyntaxTree.ParseText(_support, path: "Support.cs"), CSharpSyntaxTree.ParseText(source, path: "UserCode.cs")],
            _references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return await compilation.WithAnalyzers([new SinkTaintAnalyzer()], new AnalyzerOptions([]))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }
}
