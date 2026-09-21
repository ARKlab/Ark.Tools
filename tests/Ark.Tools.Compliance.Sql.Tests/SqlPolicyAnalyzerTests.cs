// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using Ark.Tools.Compliance.Analyzers;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Sql.Tests;

/// <summary>Verifies storage-policy enforcement and explicit transport purposes.</summary>
[TestClass]
public sealed class SqlPolicyAnalyzerTests
{
    /// <summary>Classification alone does not require SQL metadata.</summary>
    [TestMethod]
    public async Task UnmappedClassifiedTypeHasNoSqlDiagnostic()
    {
        var diagnostics = await _diagnosticsAsync("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>A missing member policy is an error only after the type opts in.</summary>
    [TestMethod]
    public async Task OptedInClassifiedMemberRequiresColumnPolicy()
    {
        var diagnostics = await _diagnosticsAsync("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007" && d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>A classified field on an opted-in type requires an explicit column policy.</summary>
    [TestMethod]
    public async Task OptedInClassifiedFieldRequiresColumnPolicy()
    {
        var diagnostics = await _diagnosticsAsync("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { [PersonalData] public string Email = ""; }
            """).ConfigureAwait(false);

        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007" && d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>A complete field mapping satisfies the SQL policy requirement.</summary>
    [TestMethod]
    public async Task CompleteClassifiedFieldPolicyHasNoDiagnostic()
    {
        var diagnostics = await _diagnosticsAsync("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked)]
                public string Email = "";
            }
            """).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>A nullable sensitive value object is classified without duplicating attributes.</summary>
    [TestMethod]
    public async Task SensitiveValueTypeRequiresColumnPolicy()
    {
        var diagnostics = await _diagnosticsAsync("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { public EmailAddress? Email { get; set; } }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007");
    }

    /// <summary>Explicit opt-out remains a valid, reviewable declaration.</summary>
    [TestMethod]
    public async Task ExplicitStorageNoneSatisfiesColumnPolicy()
    {
        var diagnostics = await _diagnosticsAsync("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.None)]
                public string Email { get; set; } = "";
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Inherited classified storage members cannot escape an opted-in mapping.</summary>
    [TestMethod]
    public async Task InheritedClassifiedMembersRequireColumnPolicy()
    {
        var diagnostics = await _diagnosticsAsync("""
            public class BaseCustomer { [PersonalData] public string Email { get; set; } = ""; }
            [SqlDataPolicy(Table = "Customers")] public class Customer : BaseCustomer { }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007");
    }

    /// <summary>The build opt-out disables storage diagnostics.</summary>
    [TestMethod]
    public async Task ComplianceOptOutSuppressesSqlDiagnostics()
    {
        const string source = """
            [SqlDataPolicy(Table = "Customers")] public class Customer
            {
                [PersonalData] public string Email { get; set; } = "";
            }
            """;
        var enabledDiagnostics = await _diagnosticsAsync(source).ConfigureAwait(false);
        enabledDiagnostics.Should().ContainSingle(static diagnostic =>
            diagnostic.Id == "ARKPII007" && diagnostic.Severity == DiagnosticSeverity.Error);

        var diagnostics = await _diagnosticsAsync(source, enabled: false).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> _diagnosticsAsync(string source, bool enabled = true)
    {
        var compilation = SqlTestCompilation._create(source);
        compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SqlPolicyAnalyzer()),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new SqlTestOptionsProvider(enabled)))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }
}
