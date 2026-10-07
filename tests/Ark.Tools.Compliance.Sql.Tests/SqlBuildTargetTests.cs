// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
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
        var result = await _runAsync("""
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
        var result = await _runAsync("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
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
        var result = await _runAsync("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
            [("Schema", value)]).ConfigureAwait(false);
        result.ExitCode.Should().NotBe(0);
        result.Log.Should().Contain("Invalid or duplicate ArkComplianceSqlToken");
        result.Sql.Should().BeEmpty();
    }

    /// <summary>Ambiguous duplicate replacements fail rather than picking a value.</summary>
    [TestMethod]
    public async Task DuplicateTokenItemsFailTheBuild()
    {
        var result = await _runAsync("ALTER TABLE [$(Schema)].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');",
            [("Schema", "one"), ("Schema", "two")]).ConfigureAwait(false);
        result.ExitCode.Should().NotBe(0);
        result.Log.Should().Contain("Invalid or duplicate ArkComplianceSqlToken");
    }

    /// <summary>
    /// The standalone entry point returns the generated scripts as full paths, so a database project
    /// can collect them without passing global properties.
    /// </summary>
    [TestMethod]
    public async Task StandaloneTargetReturnsGeneratedScriptsAsFullPaths()
    {
        var directory = Path.Join(AppContext.BaseDirectory, "SqlTargetRuns", Guid.NewGuid().ToString("N"));
        var generatorDirectory = Path.Join(directory, "generated", "Ark.Tools.Compliance.Generators",
            "Ark.Tools.Compliance.Generators.SqlPolicyGenerator");
        Directory.CreateDirectory(generatorDirectory);
        try
        {
            await File.WriteAllTextAsync(Path.Join(generatorDirectory, "ArkComplianceSql.manifest.g.cs"),
                "// ArkComplianceSqlTemplate:" + _fileName + ":"
                + Convert.ToBase64String(Encoding.UTF8.GetBytes("ALTER TABLE [dbo].[Customers] ALTER COLUMN [email] ADD MASKED WITH (FUNCTION = 'email()');"))
                + "\n").ConfigureAwait(false);
            // The caller mirrors a database project: no Properties on the MSBuild call, outputs read from TargetOutputs.
            var project = new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("EnableArkToolsCompliance", "true"),
                    new XElement("IntermediateOutputPath", "obj/"),
                    new XElement("CompilerGeneratedFilesOutputPath", "generated")),
                new XElement("Import", new XAttribute("Project",
                    Path.Join(AppContext.BaseDirectory, "Fixtures", "Ark.Tools.Compliance.Sql.targets"))),
                new XElement("Target", new XAttribute("Name", "Compile")),
                new XElement("Target", new XAttribute("Name", "Collect"),
                    new XElement("MSBuild",
                        new XAttribute("Projects", "$(MSBuildProjectFullPath)"),
                        new XAttribute("Targets", "ArkGenerateComplianceSqlStandalone"),
                        new XElement("Output", new XAttribute("TaskParameter", "TargetOutputs"),
                            new XAttribute("ItemName", "_Policy"))),
                    new XElement("WriteLinesToFile",
                        new XAttribute("File", "outputs.txt"),
                        new XAttribute("Lines", "@(_Policy)"),
                        new XAttribute("Overwrite", "true")))));
            var projectPath = Path.Join(directory, "database.proj");
            project.Save(projectPath);

            var (exitCode, log) = await _runMsBuildAsync(directory, projectPath, "-t:Collect").ConfigureAwait(false);

            exitCode.Should().Be(0, log);
            var outputs = await File.ReadAllLinesAsync(Path.Join(directory, "outputs.txt")).ConfigureAwait(false);
            outputs.Should().ContainSingle();
            Path.IsPathFullyQualified(outputs[0]).Should().BeTrue(outputs[0]);
            Path.GetFileName(outputs[0]).Should().Be(_fileName);
            (await File.ReadAllTextAsync(outputs[0]).ConfigureAwait(false)).Should().Contain("ADD MASKED WITH (FUNCTION = 'email()')");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Log, string Sql)> _runAsync(string template, (string Name, string Value)[] tokens)
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
            var (exitCode, log) = await _runMsBuildAsync(directory, projectPath, "-t:Render",
                "-p:TemplateFile=" + manifest, "-p:SqlOutputDirectory=" + directory).ConfigureAwait(false);
            var path = Path.Join(directory, Path.GetFileName(_fileName));
            var sql = File.Exists(path) ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : string.Empty;
            return (exitCode, log, sql);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Log)> _runMsBuildAsync(string directory, string projectPath, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = directory,
        };
        start.Environment["TMPDIR"] = directory;
        foreach (var argument in new[] { "msbuild", projectPath, "-nologo", "-verbosity:quiet" }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
    }
}
