// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Xml.Linq;

using AwesomeAssertions;


namespace Ark.Tools.Compliance.Sql.Tests;

/// <summary>Executes the packaged MSBuild target to verify actual substitution and artifact lifecycle.</summary>
[TestClass]
public sealed class SqlBuildTargetTests
{
    private const string _fileName = "policy-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.compliance.sql";

    /// <summary>A single value receives different escaping in identifier and literal contexts.</summary>
    [TestMethod]
    public async Task TokensAreEscapedForEachSqlContext()
    {
        var result = await _run("""
            ADD SENSITIVITY CLASSIFICATION TO [$(Shared)].[Customers].[email_address]
                WITH (LABEL = '$(Shared)', INFORMATION_TYPE = 'Contact', RANK = HIGH);
            """, [("Shared", "tenant]'; DROP TABLE Customers;--")]).ConfigureAwait(false);
        result.ExitCode.Should().Be(0, result.Log);
        result.Sql.Should().Contain("[tenant]]'; DROP TABLE Customers;--].[Customers]")
            .And.Contain("LABEL = 'tenant]''; DROP TABLE Customers;--'")
            .And.NotContain("$(Shared)");
    }

    /// <summary>Unspecified items leave SQLCMD variables unresolved.</summary>
    [TestMethod]
    public async Task MissingItemsPreserveSqlcmdVariables()
    {
        var result = await _run("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
            []).ConfigureAwait(false);
        result.ExitCode.Should().Be(0, result.Log);
        result.Sql.Should().Contain("[$(Schema)]");
    }

    /// <summary>Replacements cannot introduce new SQLCMD directives or recursive variables.</summary>
    [TestMethod]
    [DataRow("$(Other)")]
    [DataRow("tenant\n:!! destructive-command")]
    public async Task UnsafeTokenValuesFailTheBuild(string value)
    {
        var result = await _run("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
            [("Schema", value)]).ConfigureAwait(false);
        result.ExitCode.Should().NotBe(0);
        result.Log.Should().Contain("Invalid or duplicate ArkComplianceSqlToken");
        result.Sql.Should().BeEmpty();
    }

    /// <summary>Ambiguous duplicate replacements fail rather than picking a value.</summary>
    [TestMethod]
    public async Task DuplicateTokenItemsFailTheBuild()
    {
        var result = await _run("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
            [("Schema", "one"), ("Schema", "two")]).ConfigureAwait(false);
        result.ExitCode.Should().NotBe(0);
        result.Log.Should().Contain("Invalid or duplicate ArkComplianceSqlToken");
    }

    private static async Task<(int ExitCode, string Log, string Sql)> _run(string template, (string Name, string Value)[] tokens)
    {
        var directory = Path.Join(AppContext.BaseDirectory, "SqlTargetRuns", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var manifest = Path.Join(directory, "manifest.g.cs");
            await File.WriteAllTextAsync(manifest,
                "// ArkComplianceSqlTemplate:" + _fileName + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(template)) + "\n")
                .ConfigureAwait(false);
            var fixturesDirectory = Path.Join(AppContext.BaseDirectory, "Fixtures");
            var project = XDocument.Load(Path.Join(fixturesDirectory, "SqlTokens.proj"));
            project.Root!.Element("Import")!.SetAttributeValue("Project",
                Path.Join(fixturesDirectory, "Ark.Tools.Compliance.Sql.targets"));
            project.Root.Add(new XElement("ItemGroup", tokens.Select(static token =>
                new XElement("ArkComplianceSqlToken", new XAttribute("Include", token.Name),
                    new XAttribute("Value", token.Value.Replace("%", "%25", StringComparison.Ordinal)
                        .Replace("$", "%24", StringComparison.Ordinal))))));
            var projectPath = Path.Join(directory, "tokens.proj");
            project.Save(projectPath);
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory,
            };
            start.Environment["TMPDIR"] = directory;
            foreach (var argument in new[]
                     {
                         "msbuild", projectPath, "-t:Render", "-nologo", "-verbosity:quiet",
                         "-p:TemplateFile=" + manifest, "-p:SqlOutputDirectory=" + directory,
                     })
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            var log = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
            var path = Path.Join(directory, Path.GetFileName(_fileName));
            var sql = File.Exists(path) ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : string.Empty;
            return (process.ExitCode, log, sql);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
