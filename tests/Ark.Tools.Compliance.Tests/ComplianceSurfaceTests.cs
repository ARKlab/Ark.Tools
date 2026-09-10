// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.Generators;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;

namespace Ark.Tools.Compliance.Tests;

/// <summary>Verifies the deterministic privacy inventory and reviewed-baseline diagnostics.</summary>
[TestClass]
public sealed class ComplianceSurfaceTests
{
    private const string _personalMember = """
        using Ark.Tools.Compliance;
        namespace Example;
        public class Customer
        {
            [PersonalData] public string Email { get; set; } = "";
        }
        """;

    /// <summary>Repeated runs, declaration ordering, and target framework metadata do not change the surface.</summary>
    [TestMethod]
    public void Surface_IsDeterministicAcrossRunsOrderingAndTargetFrameworks()
    {
        const string source = """
            using Ark.Tools.Compliance;
            namespace Example;
            public partial class Customer
            {
                [PersonalData] public string Z { get; set; } = "";
                [Secret] public string A { get; set; } = "";
            }
            """;
        var first = _run(source);
        var second = _run(source);
        var net8 = _run(source.Replace("namespace Example;",
            "[assembly:System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v8.0\")]\nnamespace Example;", StringComparison.Ordinal));
        var net10 = _run(source.Replace("namespace Example;",
            "[assembly:System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v10.0\")]\nnamespace Example;", StringComparison.Ordinal));
        var reordered = _run("""
            using Ark.Tools.Compliance;
            namespace Example;
            public partial class Customer
            {
                [Secret] public string A { get; set; } = "";
                [PersonalData] public string Z { get; set; } = "";
            }
            """);

        first.Text.Should().Be(second.Text).And.Be(net8.Text).And.Be(net10.Text).And.Be(reordered.Text);
        first.Text.IndexOf("\tA\t", StringComparison.Ordinal)
            .Should().BeLessThan(first.Text.IndexOf("\tZ\t", StringComparison.Ordinal));
        first.Text.Should().NotContain("\r");
    }

    /// <summary>A missing baseline fails only when the gate is enabled and classified members exist.</summary>
    [TestMethod]
    public void Surface_MissingBaselineIsGatedAndEmptyCompilationIsAllowed()
    {
        _run(_personalMember, enabled: true).Diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("ARKPII020");
        _run(_personalMember).Diagnostics.Should().BeEmpty();
        _run("public class Empty { }", enabled: true).Diagnostics.Should().BeEmpty();
    }

    /// <summary>The global compliance opt-out overrides an explicitly enabled surface gate.</summary>
    [TestMethod]
    public void Surface_GlobalComplianceOptOutDisablesDiagnostics()
    {
        _run(_personalMember, enabled: true, complianceEnabled: false).Diagnostics.Should().BeEmpty();
        _run(_personalMember, "invalid baseline", enabled: true, complianceEnabled: false).Diagnostics.Should().BeEmpty();
    }

    /// <summary>The update build emits the inventory without blocking the explicit acceptance operation.</summary>
    [TestMethod]
    public void Surface_UpdateModeSuppressesOnlyBaselineDiagnostics()
    {
        var result = _run(_personalMember, enabled: true, updating: true);

        result.Diagnostics.Should().BeEmpty();
        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tEmail\tArk:PersonalData");
    }

    /// <summary>A reviewed snapshot accepts the same compilation, including CRLF baselines.</summary>
    [TestMethod]
    public void Surface_AcceptsMatchingBaselineAndLineEndings()
    {
        var baseline = _run(_personalMember).Text;

        _run(_personalMember, baseline, enabled: true).Diagnostics.Should().BeEmpty();
        _run(_personalMember, baseline.Replace("\n", "\r\n", StringComparison.Ordinal), enabled: true)
            .Diagnostics.Should().BeEmpty();
        _run(_personalMember, "\uFEFF/*\n" + baseline + "*/\n", enabled: true).Diagnostics.Should().BeEmpty();
    }

    /// <summary>A newly classified member requires a reviewed update and cannot be omitted from the baseline.</summary>
    [TestMethod]
    public void Surface_NewOrOmittedClassifiedMemberIsReported()
    {
        var result = _run(_personalMember, "COMPLIANCE-SURFACE 1\n", enabled: true);

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal("ARKPII020", "ARKPII021");
        _run(_personalMember, result.Text, enabled: true).Diagnostics.Should().BeEmpty();
    }

    /// <summary>Weakening personal information to a pseudonymous classification is a distinct privacy error.</summary>
    [TestMethod]
    public void Surface_WeakeningClassificationIsReported()
    {
        var baseline = _run(_personalMember).Text;
        var result = _run(_personalMember.Replace("[PersonalData]", "[Pseudonymous]", StringComparison.Ordinal),
            baseline, enabled: true);

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal("ARKPII020", "ARKPII021");
    }

    /// <summary>Credentials and special-category data are not interchangeable classifications.</summary>
    [TestMethod]
    public void Surface_ChangingSecretToSensitivePersonalDataIsNotStrengthening()
    {
        var baseline = _run(_personalMember.Replace("[PersonalData]", "[Secret]", StringComparison.Ordinal)).Text;
        var result = _run(_personalMember.Replace("[PersonalData]", "[SensitivePersonalData]", StringComparison.Ordinal),
            baseline, enabled: true);

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("ARKPII021");
    }

    /// <summary>Strengthening protection is drift but is not a weakened classification.</summary>
    [TestMethod]
    public void Surface_StrengtheningClassificationRequiresReviewWithoutWeakeningError()
    {
        var baseline = _run(_personalMember).Text;
        var result = _run(_personalMember.Replace("[PersonalData]", "[SensitivePersonalData]", StringComparison.Ordinal),
            baseline, enabled: true);

        result.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII020");
    }

    /// <summary>Removing a classification differs from deleting the member itself.</summary>
    [TestMethod]
    public void Surface_RemovingClassificationIsReportedButDeletingMemberIsOrdinaryDrift()
    {
        var baseline = _run(_personalMember).Text;

        _run(_personalMember.Replace("[PersonalData]", string.Empty, StringComparison.Ordinal), baseline, enabled: true)
            .Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal("ARKPII020", "ARKPII021");
        _run("namespace Example; public class Customer { }", baseline, enabled: true)
            .Diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII020");
    }

    /// <summary>Malformed and ambiguous baselines fail closed.</summary>
    [TestMethod]
    public void Surface_RejectsMalformedDuplicateAndMultipleBaselines()
    {
        var baseline = _run(_personalMember).Text;
        var line = baseline.Split('\n')[1];
        foreach (var malformed in new[]
        {
            string.Empty, "COMPLIANCE-SURFACE 2\n", baseline + line + "\n",
            baseline.Replace("Ark:PersonalData", "Unknown", StringComparison.Ordinal),
        })
        {
            _run(_personalMember, malformed, enabled: true).Diagnostics.Should().ContainSingle()
                .Which.Id.Should().Be("ARKPII020");
        }
        _run(_personalMember, baseline, enabled: true, duplicateBaseline: true)
            .Diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII020");
    }

    /// <summary>Fields, positional records, nullable values, collections, and inherited classifications are inventoried.</summary>
    [TestMethod]
    public void Surface_TracksClassificationsWithoutBackingFieldDuplicates()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            using System.Collections.Generic;
            namespace Example;
            public record Customer(EmailAddress Email);
            public class Other
            {
                public EmailAddress? Optional { get; set; }
                public List<EmailAddress> Addresses { get; set; } = new();
                [Secret] public string Field = "";
            }
            public class Base
            {
                [PersonalData] public virtual string Value { get; set; } = "";
            }
            public class Derived : Base
            {
                public override string Value { get; set; } = "";
            }
            """);

        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tEmail\tArk:PersonalData");
        result.Text.Should().Contain("CLASSIFIED\tExample.Other\tOptional\tArk:PersonalData");
        result.Text.Should().Contain("CLASSIFIED\tExample.Other\tAddresses\tArk:PersonalData");
        result.Text.Should().Contain("CLASSIFIED\tExample.Other\tField\tArk:Secret");
        result.Text.Should().Contain("CLASSIFIED\tExample.Derived\tValue\tArk:PersonalData");
        result.Text.Should().NotContain("BackingField");
    }

    /// <summary>Unrelated attributes with the same short name do not classify data.</summary>
    [TestMethod]
    public void Surface_IgnoresUnrelatedAttributeNames()
    {
        var result = _run("""
            namespace Example;
            public sealed class PersonalDataAttribute : System.Attribute { }
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """);

        result.Text.Should().Be("COMPLIANCE-SURFACE 1\n");
    }

    /// <summary>Purpose-gated reveal calls and documentation produce stable, safely escaped notes.</summary>
    [TestMethod]
    public void Surface_IncludesPurposeNotesAndEscapesCommentTerminators()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            namespace Example;
            public class Customer
            {
                /// <summary>Contact address; needed for order confirmation.</summary>
                public EmailAddress Email { get; set; }
                public string Send() => Email.Reveal(CompliancePurpose.SendTransactionalEmail);
                public string Export() => Email.Reveal(CompliancePurpose.Custom("ticket\tARK-1 */\nreview"));
            }
            """);

        result.Text.Should().Contain("Contact address; needed for order confirmation.");
        result.Text.Should().Contain("Reveal: SendTransactionalEmail");
        result.Text.Should().Contain("Reveal: ticket\\tARK-1 *\\/\\nreview");
        _run("""
            using Ark.Tools.Compliance;
            namespace Example;
            public class Customer { public EmailAddress Email { get; set; } }
            """, result.Text, enabled: true).Diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("ARKPII020");
    }

    /// <summary>PII-IMP-03 serializer registrations appear on the classified member's inventory line.</summary>
    [TestMethod]
    public void Surface_RecordsRegisteredSerializerEgress()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            using Ark.Tools.Compliance.Dapper;
            using Ark.Tools.Compliance.NewtonsoftJson;
            using Ark.Tools.Compliance.MessagePack;
            using Ark.Tools.Compliance.Protobuf;
            namespace Example;
            public class Customer
            {
                public EmailAddress Email { get; set; }
                public PhoneNumber Phone { get; set; }
                public static void Configure()
                {
                    SensitiveValueDapper.Register<EmailAddress>();
                    SensitiveValueNewtonsoftJson.Register<EmailAddress>(new Newtonsoft.Json.JsonSerializerSettings());
                    SensitiveValueFormatterResolver.Register<EmailAddress>();
                    ProtoBuf.Meta.RuntimeTypeModel.Create().Register<EmailAddress>();
                }
            }
            """);

        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tEmail\tArk:PersonalData\t\tDapper,MessagePack,Newtonsoft.Json,Protobuf,System.Text.Json");
        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tPhone\tArk:PersonalData\t\tSystem.Text.Json");
    }

    /// <summary>Ignore attributes suppress the corresponding serializer without erasing the classified member.</summary>
    [TestMethod]
    public void Surface_RespectsSerializerIgnoreAttributes()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            using Ark.Tools.Compliance.NewtonsoftJson;
            namespace Example;
            public class Customer
            {
                [System.Text.Json.Serialization.JsonIgnore]
                [Newtonsoft.Json.JsonIgnore]
                public EmailAddress Hidden { get; set; }
                [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
                public EmailAddress? Visible { get; set; }
                public static void Configure() => SensitiveValueNewtonsoftJson.RegisterBuiltIn(new Newtonsoft.Json.JsonSerializerSettings());
            }
            """);

        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tHidden\tArk:PersonalData\t\t\n");
        result.Text.Should().Contain("CLASSIFIED\tExample.Customer\tVisible\tArk:PersonalData\t\tNewtonsoft.Json,System.Text.Json");
    }

    /// <summary>Transport contracts carry their egress through nested DTOs and query result types.</summary>
    [TestMethod]
    public void Surface_RecordsHttpResultAndNestedDtoEgress()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            namespace Ark.Tools.MediatorFramework
            {
                public sealed class HttpEndpointAttribute : System.Attribute { }
                public interface IQuery<T> { }
            }
            namespace Example
            {
                [Ark.Tools.MediatorFramework.HttpEndpoint]
                public sealed class GetCustomer : Ark.Tools.MediatorFramework.IQuery<Customer> { }
                public class Customer { public Contact Contact { get; set; } = new(); }
                public class Contact { public EmailAddress Email { get; set; } }
            }
            """);

        result.Text.Should().Contain("CLASSIFIED\tExample.Contact\tEmail\tArk:PersonalData\t\tHttp:Example.GetCustomer,System.Text.Json");
    }

    /// <summary>Ignored DTO paths do not produce false transport egress, and recursive generic graphs terminate.</summary>
    [TestMethod]
    public void Surface_StopsRecursiveContractsAndIgnoresHiddenDtoPaths()
    {
        var result = _run("""
            using Ark.Tools.Compliance;
            namespace Ark.Tools.MediatorFramework
            {
                public sealed class HttpEndpointAttribute : System.Attribute { }
                public interface IQuery<T> { }
            }
            namespace Example
            {
                [Ark.Tools.MediatorFramework.HttpEndpoint]
                public sealed class GetCustomer : Ark.Tools.MediatorFramework.IQuery<Customer> { }
                public class Customer
                {
                    [System.Text.Json.Serialization.JsonIgnore]
                    public Contact Hidden { get; set; } = new();
                    public Node<string> Chain { get; set; } = new();
                }
                public class Contact { public EmailAddress Email { get; set; } }
                public class Node<T> { public Node<System.Collections.Generic.List<T>>? Next { get; set; } }
            }
            """);

        result.Text.Should().Contain("CLASSIFIED\tExample.Contact\tEmail\tArk:PersonalData\t\tSystem.Text.Json");
        result.Text.Should().NotContain("Http:");
    }

    /// <summary>Real MSBuild acceptance produces identical net8/net10 baselines for manual review.</summary>
    [TestMethod]
    public async Task Surface_TargetsRejectDriftAndAcceptReviewedMultiTargetBaseline()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Ark.Tools.slnx")))
            repository = repository.Parent;
        repository.Should().NotBeNull();
        var targets = Path.Combine(repository!.FullName, "src", "compliance", "Ark.Tools.Compliance.Generators",
            "buildTransitive", "Ark.Tools.Compliance.Surface.targets");
        var directory = Path.Combine(AppContext.BaseDirectory, "SurfaceTargetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "SurfaceFixture.csproj");
            var declaration = Path.Combine(directory, "Customer.cs");
            await File.WriteAllTextAsync(project, $$"""
                <Project>
                  <PropertyGroup>
                    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
                    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
                    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
                  </PropertyGroup>
                  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                  <ItemGroup>
                    <Analyzer Include="{{SecurityElement.Escape(typeof(ComplianceSurfaceGenerator).Assembly.Location)}}" />
                  </ItemGroup>
                  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                  <Import Project="{{SecurityElement.Escape(targets)}}" />
                </Project>
                """).ConfigureAwait(false);
            const string source = """
                namespace Ark.Tools.Compliance
                {
                    public sealed class PersonalDataAttribute : System.Attribute { }
                }
                namespace Example
                {
                    public class Customer
                    {
                        [Ark.Tools.Compliance.PersonalData] public string Email { get; set; }
                    }
                }
                """;
            await File.WriteAllTextAsync(declaration, source).ConfigureAwait(false);

            var missing = await _buildFixture(project).ConfigureAwait(false);
            missing.ExitCode.Should().NotBe(0);
            missing.Output.Should().Contain("ARKPII020");

            var generation = await _buildFixture(project, properties: ["ArkComplianceSurfaceUpdating=true"]).ConfigureAwait(false);
            generation.ExitCode.Should().Be(0, generation.Output);
            var net8 = await File.ReadAllBytesAsync(Path.Combine(directory, "obj", "Debug", "net8.0", "ArkComplianceSurface.current.txt")).ConfigureAwait(false);
            var net10 = await File.ReadAllBytesAsync(Path.Combine(directory, "obj", "Debug", "net10.0", "ArkComplianceSurface.current.txt")).ConfigureAwait(false);
            net8.Should().Equal(net10);
            await File.WriteAllBytesAsync(Path.Combine(directory, "ArkComplianceSurface.txt"), net8).ConfigureAwait(false);
            var accepted = await _buildFixture(project).ConfigureAwait(false);
            accepted.ExitCode.Should().Be(0, accepted.Output);

            await File.WriteAllTextAsync(declaration, source.Replace("public string Email", "public string Contact", StringComparison.Ordinal)).ConfigureAwait(false);
            var drift = await _buildFixture(project).ConfigureAwait(false);
            drift.ExitCode.Should().NotBe(0);
            drift.Output.Should().Contain("ARKPII020");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> _buildFixture(
        string project,
        string? target = null,
        string[]? properties = null)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("--verbosity");
        start.ArgumentList.Add("quiet");
        if (target is not null)
        {
            start.ArgumentList.Add("--target");
            start.ArgumentList.Add(target);
            start.ArgumentList.Add("--no-restore");
        }
        if (properties is not null)
        {
            foreach (var property in properties)
                start.ArgumentList.Add($"-p:{property}");
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        return (process.ExitCode, await standardOutput.ConfigureAwait(false) + await standardError.ConfigureAwait(false));
    }

    private static (string Text, ImmutableArray<Diagnostic> Diagnostics) _run(string source,
        string? baseline = null, bool enabled = false, bool updating = false, bool duplicateBaseline = false,
        bool complianceEnabled = true)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("SurfaceTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(documentationMode: DocumentationMode.Parse))],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var files = baseline is null ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new BaselineText("ArkComplianceSurface.txt", baseline));
        if (duplicateBaseline)
            files = files.Add(new BaselineText("other/ArkComplianceSurface.txt", baseline!));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ComplianceSurfaceGenerator().AsSourceGenerator()], additionalTexts: files,
            optionsProvider: new OptionsProvider(enabled, updating, complianceEnabled));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult();
        result.Results.Should().OnlyContain(static generator => generator.Exception == null);
        output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var generated = result.Results.Single().GeneratedSources.Single().SourceText.ToString();
        generated.Should().StartWith("/*\n").And.EndWith("*/\n");
        return (generated[3..^3], result.Diagnostics);
    }

    private sealed class BaselineText(string path, string text) : AdditionalText
    {
        /// <inheritdoc />
        public override string Path => path;

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(text, Encoding.UTF8);
        }
    }

    private sealed class OptionsProvider(bool enabled, bool updating, bool complianceEnabled) : AnalyzerConfigOptionsProvider
    {
        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(enabled, updating, complianceEnabled);

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return GlobalOptions;
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return GlobalOptions;
        }
    }

    private sealed class Options(bool enabled, bool updating, bool complianceEnabled) : AnalyzerConfigOptions
    {
        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value)
        {
            value = key switch
            {
                "build_property.EnableArkToolsCompliance" => complianceEnabled ? "true" : "false",
                "build_property.ArkComplianceSurfaceEnabled" => enabled ? "true" : "false",
                "build_property.ArkComplianceSurfaceUpdating" => updating ? "true" : "false",
                _ => string.Empty,
            };
            return value.Length > 0;
        }
    }
}
