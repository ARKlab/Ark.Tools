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
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>A missing member policy is an error only after the type opts in.</summary>
    [TestMethod]
    public async Task OptedInClassifiedMemberRequiresColumnPolicy()
    {
        var diagnostics = await _diagnostics("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007" && d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>A nullable sensitive value object is classified without duplicating attributes.</summary>
    [TestMethod]
    public async Task SensitiveValueTypeRequiresColumnPolicy()
    {
        var diagnostics = await _diagnostics("""
            [SqlDataPolicy(Table = "Customers")]
            public class Customer { public EmailAddress? Email { get; set; } }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007");
    }

    /// <summary>Explicit opt-out remains a valid, reviewable declaration.</summary>
    [TestMethod]
    public async Task ExplicitStorageNoneSatisfiesColumnPolicy()
    {
        var diagnostics = await _diagnostics("""
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
        var diagnostics = await _diagnostics("""
            public class BaseCustomer { [PersonalData] public string Email { get; set; } = ""; }
            [SqlDataPolicy(Table = "Customers")] public class Customer : BaseCustomer { }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII007");
    }

    /// <summary>JSON egress without SQL metadata still requires a declared purpose.</summary>
    [TestMethod]
    public async Task JsonEgressWithoutPurposeWarns()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static string Send(Customer value) => System.Text.Json.JsonSerializer.Serialize(value);
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012" && d.Severity == DiagnosticSeverity.Warning);
    }

    /// <summary>Anonymous-object payloads carrying classified members still require a declared purpose.</summary>
    [TestMethod]
    public async Task AnonymousPayloadWithClassifiedMemberWarns()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static string Send(Customer value) => System.Text.Json.JsonSerializer.Serialize(new { value.Email });
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012" && d.Severity == DiagnosticSeverity.Warning);
    }

    /// <summary>A lawful-purpose declaration on the contract satisfies the transport rule.</summary>
    [TestMethod]
    public async Task DeclaredContractPurposeSilencesEgressWarning()
    {
        var diagnostics = await _diagnostics("""
            [PersonalDataEgress(Purpose = "customer support response")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static string Send(Customer value) => System.Text.Json.JsonSerializer.Serialize(value);
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>A declaration at a transport boundary also records a purpose.</summary>
    [TestMethod]
    public async Task DeclaredMethodPurposeSilencesEgressWarning()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                [PersonalDataEgress(Purpose = "customer support response")]
                public static string Send(Customer value) => System.Text.Json.JsonSerializer.Serialize(value);
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>An empty declaration does not silently permit a classified egress.</summary>
    [TestMethod]
    public async Task EmptyPurposeDoesNotSilenceEgressWarning()
    {
        var diagnostics = await _diagnostics("""
            [PersonalDataEgress(Purpose = " ")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static string Send(Customer value) => System.Text.Json.JsonSerializer.Serialize(value);
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012");
    }

    /// <summary>Declared policies on collection element contracts apply through generic wrappers.</summary>
    [TestMethod]
    [DataRow("System.Collections.Generic.List<Customer>")]
    [DataRow("Customer[]")]
    public async Task DeclaredCollectionElementPurposeSilencesEgressWarning(string collectionType)
    {
        var diagnostics = await _diagnostics("""
            [PersonalDataEgress(Purpose = "customer support response")]
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static string Send(COLLECTION_TYPE values)
                    => System.Text.Json.JsonSerializer.Serialize(values);
            }
            """.Replace("COLLECTION_TYPE", collectionType, StringComparison.Ordinal)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Message transport is checked semantically, independent of SQL mappings.</summary>
    [TestMethod]
    public async Task MessageEgressWithoutPurposeWarns()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public static class Transport
            {
                public static void Send(Rebus.Bus.IBus bus, Customer value) => bus.Publish(value);
            }
            namespace Rebus.Bus
            {
                public interface IBus { void Publish(object message); }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012");
    }

    /// <summary>HTTP action results are checked even when no serializer call is visible.</summary>
    [TestMethod]
    public async Task HttpEgressWithoutPurposeWarns()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            public class CustomerController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                public Customer Get() => new Customer();
            }
            namespace Microsoft.AspNetCore.Mvc { public class ControllerBase { } }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012");
    }

    /// <summary>Directly serializing a classified scalar member still constitutes egress.</summary>
    [TestMethod]
    public async Task ClassifiedScalarEgressWarns()
    {
        var diagnostics = await _diagnostics("""
            public class Customer
            {
                [PersonalData] public string Email { get; set; } = "";
                public string Send() => System.Text.Json.JsonSerializer.Serialize(Email);
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012");
    }

    /// <summary>Ark HTTP query response contracts are checked once, including self-typed query interfaces.</summary>
    [TestMethod]
    public async Task ArkEndpointResponseHasOneEgressWarning()
    {
        var diagnostics = await _diagnostics("""
            public class Customer { [PersonalData] public string Email { get; set; } = ""; }
            [Ark.Tools.MediatorFramework.HttpEndpoint]
            public class Query : Ark.Tools.Solid.IQuery<Query, Customer> { }
            namespace Ark.Tools.Solid
            {
                public interface IQuery<T> { }
                public interface IQuery<TSelf, T> : IQuery<T> { }
            }
            namespace Ark.Tools.MediatorFramework
            {
                [AttributeUsage(AttributeTargets.Class)] public sealed class HttpEndpointAttribute : Attribute { }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static d => d.Id == "ARKPII012");
    }

    /// <summary>The build opt-out disables both storage and egress diagnostics.</summary>
    [TestMethod]
    public async Task ComplianceOptOutSuppressesSqlAndEgressDiagnostics()
    {
        var diagnostics = await _diagnostics("""
            [SqlDataPolicy(Table = "Customers")] public class Customer
            {
                [PersonalData] public string Email { get; set; } = "";
                public string Send() => System.Text.Json.JsonSerializer.Serialize(this);
            }
            """, enabled: false).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> _diagnostics(string source, bool enabled = true)
    {
        var compilation = SqlTestCompilation._create(source);
        compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SqlPolicyAnalyzer()),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new SqlTestOptionsProvider(enabled)))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }
}
