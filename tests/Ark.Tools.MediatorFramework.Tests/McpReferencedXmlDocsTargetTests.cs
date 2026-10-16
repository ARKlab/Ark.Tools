// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Diagnostics;
using System.Xml.Linq;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>
/// Executes the packaged MCP buildTransitive target against a minimal project with stubbed references,
/// to verify which documentation files it adds to <c>AdditionalFiles</c>.
/// </summary>
[TestClass]
public sealed class McpReferencedXmlDocsTargetTests
{
    /// <summary>
    /// By default only project reference documentation is added, once, as an MCP-only copy, without duplicating
    /// existing AdditionalFiles or the project's own documentation file, and never package or framework documentation.
    /// </summary>
    [TestMethod]
    public async Task DefaultAddsProjectReferenceDocumentationOnly()
    {
        var files = await _runAsync(1).ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml", "obj/ArkMcp/ProjLib.arkmcpdoc");
    }

    /// <summary>Opting out leaves AdditionalFiles unchanged.</summary>
    [TestMethod]
    public async Task OptOutAddsNothing()
    {
        var files = await _runAsync(1, "-p:ArkMcpIncludeReferencedXmlDocs=false").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml");
    }

    /// <summary>Opting in to package documentation adds it, but still not framework reference-pack documentation.</summary>
    [TestMethod]
    public async Task PackageOptInAddsPackageButNotFrameworkDocumentation()
    {
        var files = await _runAsync(1, "-p:ArkMcpIncludePackageXmlDocs=true").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml", "obj/ArkMcp/ProjLib.arkmcpdoc", "obj/ArkMcp/Pkg.arkmcpdoc");
    }

    /// <summary>
    /// Referenced documentation never reaches generators that read every .xml AdditionalFile, such as the ASP.NET Core
    /// OpenAPI XML comment generator, which fails on member ids that several referenced files document.
    /// </summary>
    [TestMethod]
    public async Task ReferencedDocumentationIsNotAddedAsXml()
    {
        var files = await _runAsync(1, "-p:ArkMcpIncludePackageXmlDocs=true").ConfigureAwait(false);
        files.Where(static file => file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Should().BeEquivalentTo("existing/Existing.xml");
    }

    /// <summary>An up-to-date build skips the copy but still adds the copies to AdditionalFiles.</summary>
    [TestMethod]
    public async Task UpToDateBuildStillAddsDocumentation()
    {
        var files = await _runAsync(2).ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml", "obj/ArkMcp/ProjLib.arkmcpdoc");
    }

    /// <summary>
    /// A reference that moves to a file with the same name but an older timestamp, such as a downgraded package,
    /// still refreshes the copy, which a newer-than check would keep stale.
    /// </summary>
    [TestMethod]
    public async Task MovedReferenceWithOlderDocumentationRefreshesCopy()
    {
        string? copy = null;
        await _runAsync([[], ["-p:ProjLibDirectory=proj-older"]],
            async directory => copy = await File.ReadAllTextAsync(Path.Join(directory, "obj/ArkMcp/ProjLib.arkmcpdoc")).ConfigureAwait(false))
            .ConfigureAwait(false);
        copy.Should().Be("<doc>older</doc>");
    }

    /// <summary>The target does nothing when the MCP generator is not among the analyzers.</summary>
    [TestMethod]
    public async Task WithoutMcpGeneratorAddsNothing()
    {
        var files = await _runAsync(1, "-p:WithoutMcpGenerator=true", "-p:ArkMcpIncludePackageXmlDocs=true").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml");
    }

    private static async Task<string[]> _runAsync(int runs, params string[] arguments)
    {
        return await _runAsync([.. Enumerable.Repeat(arguments, runs)], null).ConfigureAwait(false);
    }

    private static async Task<string[]> _runAsync(string[][] runs, Func<string, Task>? inspect)
    {
        var directory = Path.Join(AppContext.BaseDirectory, "McpTargetRuns", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var xml in new[] { "proj/ProjLib.xml", "pkg/Pkg.xml", "ref/System.Runtime.xml", "existing/Existing.xml", "own/Own.xml" })
            {
                Directory.CreateDirectory(Path.Join(directory, Path.GetDirectoryName(xml)));
                await File.WriteAllTextAsync(Path.Join(directory, xml), "<doc />").ConfigureAwait(false);
            }
            var older = Path.Join(directory, "proj-older", "ProjLib.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(older)!);
            await File.WriteAllTextAsync(older, "<doc>older</doc>").ConfigureAwait(false);
            File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-1));
            var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
            var project = new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("IntermediateOutputPath", "obj/"),
                    new XElement("ProjLibDirectory", new XAttribute("Condition", "'$(ProjLibDirectory)' == ''"), "proj")),
                new XElement("ItemGroup",
                    new XElement("Analyzer", new XAttribute("Include", "analyzers/Ark.Tools.MediatorFramework.Mcp.Generators.dll"),
                        new XAttribute("Condition", "'$(WithoutMcpGenerator)' != 'true'")),
                    new XElement("Analyzer", new XAttribute("Include", "analyzers/Other.Generators.dll")),
                    new XElement("AdditionalFiles", new XAttribute("Include", "existing/Existing.xml")),
                    new XElement("DocFileItem", new XAttribute("Include", "own/Own.xml")),
                    _reference("$(ProjLibDirectory)/ProjLib.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("$(ProjLibDirectory)/ProjLib.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("existing/Existing.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("own/Own.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("missing/Missing.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("pkg/Pkg.dll", new XElement("NuGetPackageId", "Pkg")),
                    _reference("ref/System.Runtime.dll", new XElement("NuGetPackageId", "Microsoft.NETCore.App.Ref"),
                        new XElement("FrameworkReferenceName", "Microsoft.NETCore.App"))),
                new XElement("Import", new XAttribute("Project", Path.Join(root, "src", "mediator-framework",
                    "Ark.Tools.MediatorFramework.Mcp", "buildTransitive", "Ark.Tools.MediatorFramework.Mcp.targets"))),
                // Stubs the SDK target the MCP target depends on; ReferencePath is declared statically above.
                new XElement("Target", new XAttribute("Name", "ResolveAssemblyReferences")),
                new XElement("Target", new XAttribute("Name", "Collect"), new XAttribute("DependsOnTargets", "ArkMcpAddReferencedXmlDocumentation"),
                    new XElement("WriteLinesToFile", new XAttribute("File", "outputs.txt"),
                        new XAttribute("Lines", "@(AdditionalFiles->'%(FullPath)')"), new XAttribute("Overwrite", "true")))));
            var projectPath = Path.Join(directory, "app.proj");
            project.Save(projectPath);

            foreach (var arguments in runs)
            {
                await _msbuildAsync(directory, projectPath, arguments).ConfigureAwait(false);
            }
            if (inspect is not null)
            {
                await inspect(directory).ConfigureAwait(false);
            }

            var lines = await File.ReadAllLinesAsync(Path.Join(directory, "outputs.txt")).ConfigureAwait(false);
            foreach (var copy in lines.Where(static line => line.EndsWith(".arkmcpdoc", StringComparison.Ordinal)))
            {
                File.Exists(copy).Should().BeTrue(copy);
            }
            return [.. lines.Select(line => Path.GetRelativePath(directory, line).Replace('\\', '/'))];
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task _msbuildAsync(string directory, string projectPath, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = directory,
        };
        foreach (var argument in new[] { "msbuild", projectPath, "-nologo", "-verbosity:quiet", "-t:Collect" }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var log = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
        process.ExitCode.Should().Be(0, log);
    }

    private static XElement _reference(string include, params XElement[] metadata)
    {
        return new XElement("ReferencePath", new XAttribute("Include", include), metadata);
    }
}
