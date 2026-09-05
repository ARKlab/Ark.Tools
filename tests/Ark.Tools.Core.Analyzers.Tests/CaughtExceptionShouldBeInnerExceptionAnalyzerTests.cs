// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Tests caught exception wrapping diagnostics.</summary>
[TestClass]
public class CaughtExceptionShouldBeInnerExceptionAnalyzerTests
{
    /// <summary>Verifies a wrapped exception without an inner exception is rejected.</summary>
    [TestMethod]
    public async Task WrappedExceptionWithoutInnerException_ShouldReportError()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException("failed");
                    }
                }
            }
            """);

        diagnostics.Should().ContainSingle(static item => item.Id == "ARKCORE005");
        diagnostics.Single(static item => item.Id == "ARKCORE005").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    /// <summary>Verifies direct and named inner exception arguments are accepted.</summary>
    [TestMethod]
    public async Task WrappedExceptionWithInnerException_ShouldNotReportDiagnostic()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException("failed", innerException: exception);
                    }
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Verifies logic before a wrapping throw does not hide the diagnostic.</summary>
    [TestMethod]
    public async Task ConditionalWrappingThrow_ShouldReportError()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M(bool retry)
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        if (retry)
                        {
                            throw new InvalidOperationException("failed");
                        }
                    }
                }
            }
            """);

        diagnostics.Should().ContainSingle(static item => item.Id == "ARKCORE005");
    }

    /// <summary>Verifies exceptions returned by another function are not assumed to preserve the caught exception.</summary>
    [TestMethod]
    public async Task DeferredExceptionThrow_ShouldReportError()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                Exception HandleException() => new InvalidOperationException();

                void M()
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        throw HandleException();
                    }
                }
            }
            """);

        diagnostics.Should().ContainSingle(static item => item.Id == "ARKCORE005");
    }

    /// <summary>Verifies exceptions created in the catch body must preserve the caught exception.</summary>
    [TestMethod]
    public async Task BodyCreatedException_ShouldReportError()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M(bool replace)
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        Exception replacement = null;
                        if (replace)
                        {
                            replacement = new InvalidOperationException();
                        }

                        if (replacement is not null)
                        {
                            throw replacement;
                        }

                        throw;
                    }
                }
            }
            """);

        diagnostics.Should().ContainSingle(static item => item.Id == "ARKCORE005");
    }

    /// <summary>Verifies rethrowing the caught exception is accepted.</summary>
    [TestMethod]
    public async Task RethrowingCaughtException_ShouldNotReportDiagnostic()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        throw exception;
                    }
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Verifies catch clauses without a variable report a dedicated diagnostic.</summary>
    [TestMethod]
    public async Task CatchWithoutVariable_ShouldReportCaptureDiagnostic()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    try { throw new Exception(); }
                    catch
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
            """);

        diagnostics.Should().ContainSingle(static item => item.Id == "ARKCORE006" && item.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>Verifies bare rethrows from catch clauses without variables are accepted.</summary>
    [TestMethod]
    public async Task CatchWithoutVariableBareRethrow_ShouldNotReportDiagnostic()
    {
        var diagnostics = await _analyzeAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    try { throw new Exception(); }
                    catch
                    {
                        throw;
                    }
                }
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
            .WithAnalyzers([new CaughtExceptionShouldBeInnerExceptionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }
}
