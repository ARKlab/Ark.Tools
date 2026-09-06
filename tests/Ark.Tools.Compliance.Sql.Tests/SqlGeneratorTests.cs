// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using Ark.Tools.Compliance.Generators;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.Compliance.Sql.Tests;

/// <summary>Verifies explicit mappings, deterministic templates and SQL-context escaping.</summary>
[TestClass]
public sealed class SqlGeneratorTests
{
    /// <summary>Classification alone never opts a type into SQL output.</summary>
    [TestMethod]
    public void UnmappedClassifiedTypeEmitsNoSql()
    {
        var result = _generate("public class Customer { [PersonalData] public string Email { get; set; } = \"\"; }");
        result.Sql.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    /// <summary>A type policy does not guess columns for unmapped members.</summary>
    [TestMethod]
    public void TypePolicyAloneDoesNotGuessColumn()
    {
        var result = _generate("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """);
        result.Sql.Should().BeEmpty();
    }

    /// <summary>The SQL column name survives property renames.</summary>
    [TestMethod]
    public void ColumnMappingIsVerbatimAcrossPropertyRename()
    {
        const string declaration = """
            [SqlDataPolicy(Schema = "sales", Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked, MaskFunction = SqlMask.Email)]
                public string Email { get; set; } = "";
            }
            """;
        var original = _generate(declaration);
        var renamed = _generate(declaration.Replace("string Email", "string Contact", StringComparison.Ordinal));
        original.Sql.Should().Equal(renamed.Sql);
        original.Sql.Single().Should().Contain("[sales].[Customers].[email_address]")
            .And.Contain("FUNCTION = 'email()'").And.NotContain(".[Email]");
    }

    /// <summary>Ordering depends on SQL mappings rather than member declaration order.</summary>
    [TestMethod]
    public void ReorderedMembersProduceIdenticalSql()
    {
        const string first = """[PersonalData, SqlColumnPolicy("a", StoragePolicy.None)] public string Z { get; set; } = "";""";
        const string second = """[PersonalData, SqlColumnPolicy("z", StoragePolicy.Masked)] public string A { get; set; } = "";""";
        const string prefix = """[SqlDataPolicy(Table = "Customers")] public class Customer { """;
        var original = _generate(prefix + first + second + "}");
        var reordered = _generate(prefix + second + first + "}");
        original.Sql.Should().Equal(reordered.Sql);
        original.Sql.Single().IndexOf(".[a]", StringComparison.Ordinal)
            .Should().BeLessThan(original.Sql.Single().IndexOf(".[z]", StringComparison.Ordinal));
    }

    /// <summary>Unresolved variables are preserved for SQLCMD or build-time substitution.</summary>
    [TestMethod]
    public void TemplatePreservesSqlcmdVariables()
    {
        var result = _generate("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked)]
                public string Email { get; set; } = "";
            }
            """);
        result.Sql.Single().Should().Contain("[$(ComplianceSchema)]")
            .And.Contain("LABEL = '$(ComplianceLabel)'");
    }

    /// <summary>Split mappings override the table policy without interpreting identifier punctuation.</summary>
    [TestMethod]
    public void SplitMappingsAndLiteralsAreEscaped()
    {
        var result = _generate("""
            [SqlDataPolicy(Schema = "base", Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email]; DROP TABLE X;--", StoragePolicy.Masked,
                    Schema = "sales]", Table = "Customer.Part", Label = "Customer's secret",
                    InformationType = "Contact's details", MaskFunction = "partial(1,'x',0)")]
                public string Email { get; set; } = "";
            }
            """);
        result.Sql.Single().Should().Contain("[sales]]].[Customer.Part].[email]]; DROP TABLE X;--]")
            .And.Contain("LABEL = 'Customer''s secret'")
            .And.Contain("INFORMATION_TYPE = 'Contact''s details'")
            .And.Contain("FUNCTION = 'partial(1,''x'',0)'");
    }

    /// <summary>Encryption declarations generate classification but never pretend to provision encryption.</summary>
    [TestMethod]
    public void ApplicationEncryptionDoesNotEmitMaskOrInventEncryptionDdl()
    {
        var result = _generate("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [SensitivePersonalData, SqlColumnPolicy("notes", StoragePolicy.ApplicationEncrypted, KeyName = "customer")]
                public string Notes { get; set; } = "";
            }
            """);
        result.Sql.Single().Should().Contain("RANK = CRITICAL").And.NotContain("ALTER TABLE")
            .And.NotContain("ENCRYPTED WITH");
    }

    /// <summary>Sensitive value object types carry classification into SQL policy output.</summary>
    [TestMethod]
    public void SensitiveValueTypeIsClassifiedWithoutMemberAttribute()
    {
        var result = _generate("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [SqlColumnPolicy("email_address", StoragePolicy.Masked)]
                public EmailAddress? Email { get; set; }
            }
            """);
        result.Sql.Single().Should().Contain("ADD SENSITIVITY CLASSIFICATION");
    }

    /// <summary>Missing table mappings fail rather than guessing a name.</summary>
    [TestMethod]
    public void MissingTableFailsWithDiagnostic()
    {
        var result = _generate("""
            [SqlDataPolicy]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked)]
                public string Email { get; set; } = "";
            }
            """, expectMappingError: true);
        result.Diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII207");
        result.Sql.Should().BeEmpty();
    }

    /// <summary>Ambiguous column mappings cannot silently select a storage policy.</summary>
    [TestMethod]
    public void DuplicateColumnsFailWithDiagnostic()
    {
        var result = _generate("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer
            {
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.None)] public string A { get; set; } = "";
                [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked)] public string B { get; set; } = "";
            }
            """, expectMappingError: true);
        result.Diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII207");
        result.Sql.Should().BeEmpty();
    }

    /// <summary>The package build target writes a real SQL file and safely replaces MSBuild items.</summary>
    [TestMethod]
    public void BuildTargetMaterializesEscapedSql()
    {
        var paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "ComplianceSql"), "*.compliance.sql");
        paths.Should().ContainSingle();
        var sql = File.ReadAllText(paths[0]);
        sql.Should().Contain("[tenant]]schema].[Customers].[email_address]")
            .And.Contain("LABEL = 'Customer''s confidential data'")
            .And.NotContain("$(ComplianceSchema)").And.NotContain("$(ComplianceLabel)");
    }

    private static (string[] Sql, ImmutableArray<Diagnostic> Diagnostics) _generate(string source, bool expectMappingError = false)
    {
        var compilation = SqlTestCompilation._create(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SqlPolicyGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        if (!expectMappingError)
        {
            diagnostics.Should().BeEmpty();
        }
        output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var generated = driver.GetRunResult().Results.Single().GeneratedSources;
        const string marker = "// ArkComplianceSqlTemplate:";
        var sql = generated.SelectMany(static s => s.SourceText.ToString().Split('\n'))
            .Where(static line => line.StartsWith(marker, StringComparison.Ordinal))
            .Select(static line => Encoding.UTF8.GetString(Convert.FromBase64String(line[(line.IndexOf(':', marker.Length) + 1)..])))
            .ToArray();
        return (sql, diagnostics);
    }
}
