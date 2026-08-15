// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using System.Collections.Immutable;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Tests compile-time evolvable enum diagnostics.</summary>
[TestClass]
public class EvolvableEnumAnalyzerTests
{
    /// <summary>Verifies valid default and explicit backing types produce no diagnostics.</summary>
    [TestMethod]
    public async Task ValidEnums_ShouldNotReportDiagnostics()
    {
        var diagnostics = await _analyzeAsync(
            """
            using CoreStatus = Ark.Tools.Core.EvolvableEnum<Status>;
            namespace Ark.Tools.Core
            {
                public struct EvolvableEnum<T> { }
                public struct EvolvableEnum<T, TBacking> { }
            }
            enum Status { NOT_SET = 0, Active = 1 }
            enum CompactStatus : byte { NOT_SET = 0, Active = 1 }
            class Contract
            {
                Ark.Tools.Core.EvolvableEnum<Status> Default;
                Ark.Tools.Core.EvolvableEnum<CompactStatus, byte> Compact;
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Verifies backing mismatch and missing NOT_SET are compile-time errors.</summary>
    [TestMethod]
    public async Task InvalidEnums_ShouldReportActionableErrors()
    {
        var diagnostics = await _analyzeAsync(
            """
            namespace Ark.Tools.Core
            {
                public struct EvolvableEnum<T> { }
                public struct EvolvableEnum<T, TBacking> { }
            }
            enum CompactStatus : byte { Active = 1 }
            class Contract
            {
                Ark.Tools.Core.EvolvableEnum<CompactStatus> Value;
            }
            """);

        diagnostics.Select(item => item.Id).Should().BeEquivalentTo(["ARKCORE001", "ARKCORE002"]);
        diagnostics.Should().OnlyContain(item => item.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>Verifies aliases and lookalike generic types are matched by symbol identity.</summary>
    [TestMethod]
    public async Task AliasesAndLookalikes_ShouldNotProduceFalsePositives()
    {
        var diagnostics = await _analyzeAsync(
            """
            namespace Ark.Tools.Core
            {
                public struct EvolvableEnum<T> { }
                public struct EvolvableEnum<T, TBacking> { }
            }
            namespace Other
            {
                public struct EvolvableEnum<T> { }
            }
            enum Status { NOT_SET = 0, Active = 1 }
            class Contract
            {
                CoreStatus Value;
                Other.EvolvableEnum<Status> OtherValue;
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return await compilation
            .WithAnalyzers([new EvolvableEnumAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }
}
