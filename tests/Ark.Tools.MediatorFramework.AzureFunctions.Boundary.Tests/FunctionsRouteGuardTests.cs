// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

using AwesomeAssertions;

using Microsoft.Azure.Functions.Worker;

using System.Reflection;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests;

/// <summary>
/// Guards the generated Azure Functions HTTP surface against the declared
/// <c>[HttpEndpoint]</c> inventory and an explicit expected-route fixture.
/// </summary>
[TestClass]
public sealed class FunctionsRouteGuardTests
{
    private static readonly Assembly _functionsAssembly = typeof(EchoQuery).Assembly;

    private static readonly (string Name, string Verb, string Route)[] _expectedFunctions =
    [
        ("ArkHealthCheck", "get", "healthCheck"),
        ("DownloadFileQuery_v1", "get", "api/v1/files/{name}"),
        ("EchoQuery_v1", "get", "api/v1/echo/{id}"),
        ("EchoRequest_v1", "post", "api/v1/echo"),
        ("PingQuery_v1", "get", "api/v1/ping"),
        ("ReleaseStreamRequest_v1", "post", "api/v1/stream/release"),
        ("StreamForeverQuery_v1", "get", "api/v1/stream/forever"),
        ("StreamNumbersQuery_v1", "get", "api/v1/stream"),
        ("StreamStateQuery_v1", "get", "api/v1/stream/state"),
        ("UploadFileRequest_v1", "post", "api/v1/files"),
        ("VersionedEchoRequest_v1", "put", "api/v1/versioned/{id}"),
    ];

    [TestMethod]
    public void GeneratedFunctionsMatchTheExpectedRouteFixture()
    {
        var actual = _generatedFunctions()
            .OrderBy(function => function.Name, StringComparer.Ordinal)
            .ToArray();

        actual.Should().BeEquivalentTo(
            _expectedFunctions.OrderBy(function => function.Name, StringComparer.Ordinal),
            options => options.WithStrictOrdering());
    }

    [TestMethod]
    public void GeneratedFunctionsCoverEveryNonExcludedHttpEndpointContract()
    {
        var host = _functionsAssembly.GetCustomAttributes<HttpHostAttribute>().Single();
        var contractAssembly = host.ContractAssemblyMarker.Assembly;
        var excluded = host.ExcludedContracts.ToHashSet(EqualityComparer<Type>.Default);
        var functionsByRoute = _generatedFunctions()
            .Select(function => (function.Verb, function.Route))
            .ToHashSet();

        foreach (var contract in contractAssembly.GetTypes())
        {
            var endpoint = contract.GetCustomAttribute<HttpEndpointAttribute>();
            if (endpoint is null)
                continue;

            var route = endpoint.Template
                .Replace("{version}", "1", StringComparison.Ordinal)
                .Trim('/');
            if (!endpoint.Template.Contains("{version}", StringComparison.Ordinal))
                route = "api/v1/" + route;

            var expectation = (endpoint.Verb.ToLowerInvariant(), route);
            if (excluded.Contains(contract))
                functionsByRoute.Should().NotContain(expectation, "contract {0} is excluded from the Functions host", contract);
            else
                functionsByRoute.Should().Contain(expectation, "contract {0} must be exposed by the Functions host", contract);
        }
    }

    private static IEnumerable<(string Name, string Verb, string Route)> _generatedFunctions()
    {
        foreach (var type in _functionsAssembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var function = method.GetCustomAttribute<FunctionAttribute>();
                if (function is null)
                    continue;

                var trigger = method.GetParameters()
                    .Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>())
                    .FirstOrDefault(attribute => attribute is not null);
                if (trigger is null)
                    continue;

                yield return (function.Name, trigger.Methods!.Single(), trigger.Route!);
            }
        }
    }
}
