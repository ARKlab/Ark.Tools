// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Diagnostics;
using System.Xml.Linq;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>
/// Executes the packaged MCP buildTransitive target against a minimal project with stubbed references,
/// to verify which XML documentation files it adds to <c>AdditionalFiles</c>.
/// </summary>
[TestClass]
public sealed class McpReferencedXmlDocsTargetTests
{
    /// <summary>
    /// By default only project reference documentation is added, once, without duplicating existing
    /// AdditionalFiles or the project's own documentation file, and never package or framework documentation.
    /// </summary>
    [TestMethod]
    public async Task DefaultAddsProjectReferenceDocumentationOnly()
    {
        var files = await _runAsync().ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml", "proj/ProjLib.xml");
    }

    /// <summary>Opting out leaves AdditionalFiles unchanged.</summary>
    [TestMethod]
    public async Task OptOutAddsNothing()
    {
        var files = await _runAsync("-p:ArkMcpIncludeReferencedXmlDocs=false").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml");
    }

    /// <summary>Opting in to package documentation adds it, but still not framework reference-pack documentation.</summary>
    [TestMethod]
    public async Task PackageOptInAddsPackageButNotFrameworkDocumentation()
    {
        var files = await _runAsync("-p:ArkMcpIncludePackageXmlDocs=true").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml", "proj/ProjLib.xml", "pkg/Pkg.xml");
    }

    /// <summary>The target does nothing when the MCP generator is not among the analyzers.</summary>
    [TestMethod]
    public async Task WithoutMcpGeneratorAddsNothing()
    {
        var files = await _runAsync("-p:WithoutMcpGenerator=true", "-p:ArkMcpIncludePackageXmlDocs=true").ConfigureAwait(false);
        files.Should().BeEquivalentTo("existing/Existing.xml");
    }

    private static async Task<string[]> _runAsync(params string[] arguments)
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
            var root = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
            var project = new XDocument(new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("Analyzer", new XAttribute("Include", "analyzers/Ark.Tools.MediatorFramework.Mcp.Generators.dll"),
                        new XAttribute("Condition", "'$(WithoutMcpGenerator)' != 'true'")),
                    new XElement("Analyzer", new XAttribute("Include", "analyzers/Other.Generators.dll")),
                    new XElement("AdditionalFiles", new XAttribute("Include", "existing/Existing.xml")),
                    new XElement("DocFileItem", new XAttribute("Include", "own/Own.xml")),
                    _reference("proj/ProjLib.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
                    _reference("proj/ProjLib.dll", new XElement("ReferenceSourceTarget", "ProjectReference")),
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

            var lines = await File.ReadAllLinesAsync(Path.Join(directory, "outputs.txt")).ConfigureAwait(false);
            return [.. lines.Select(line => Path.GetRelativePath(directory, line).Replace('\\', '/'))];
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static XElement _reference(string include, params XElement[] metadata)
    {
        return new XElement("ReferencePath", new XAttribute("Include", include), metadata);
    }
}
