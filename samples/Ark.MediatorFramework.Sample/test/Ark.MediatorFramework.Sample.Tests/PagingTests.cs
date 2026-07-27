// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Application;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AppGreetingPage = Ark.MediatorFramework.Sample.Application.GreetingPage;
using GrpcSearchGreetingsQuery = Ark.MediatorFramework.Sample.GrpcClient.SearchGreetingsQuery;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies paged greeting search across HTTP and gRPC.</summary>
[TestClass]
public sealed class PagingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();

    /// <summary>Returns two HTTP pages with the expected total count and boundaries.</summary>
    [TestMethod]
    public async Task HttpSearchReturnsPagesAndTotalCount()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").AddScope(ApplicationScopes.GreetingWrite).Build());

        await CreateGreetingAsync(context.Client, "page-one").ConfigureAwait(false);
        await CreateGreetingAsync(context.Client, "page-two").ConfigureAwait(false);
        await CreateGreetingAsync(context.Client, "page-three").ConfigureAwait(false);

        var first = await context.Client.GetFromJsonAsync<AppGreetingPage>(
            "/api/v1/greetings?skip=0&limit=2",
            JsonOptions).ConfigureAwait(false);
        var second = await context.Client.GetFromJsonAsync<AppGreetingPage>(
            "/api/v1/greetings?skip=2&limit=2",
            JsonOptions).ConfigureAwait(false);

        first!.Count.Should().Be(3);
        first.Data.Length.Should().Be(2);
        second!.Count.Should().Be(3);
        second.Data.Length.Should().Be(1);
        first.Data.Select(greeting => greeting.Id).Should().NotContain(second.Data[0].Id);
    }

    /// <summary>Rejects a page size outside the supported range.</summary>
    [TestMethod]
    public async Task HttpSearchRejectsInvalidLimit()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").AddScope(ApplicationScopes.GreetingWrite).Build());

        var response = await context.Client.GetAsync(
            new Uri("/api/v1/greetings?skip=0&limit=101", UriKind.Relative)).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        await File.WriteAllTextAsync("/tmp/test_response.txt", $"STATUS:{(int)response.StatusCode}\nBODY:\n{body}\nEND").ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Returns the same paged result over gRPC.</summary>
    [TestMethod]
    public async Task GrpcSearchReturnsPageAndTotalCount()
    {
        using var context = new SampleTestContext();
        var token = new JwtTokenBuilder().AddSubject("test-user").AddScope(ApplicationScopes.GreetingWrite).Build();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await CreateGreetingAsync(context.Client, "grpc-page-one").ConfigureAwait(false);
        await CreateGreetingAsync(context.Client, "grpc-page-two").ConfigureAwait(false);

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
        var result = await new GreetingsV1.GreetingsV1Client(channel).SearchGreetingsAsync(
            new GrpcSearchGreetingsQuery { Skip = 1, Limit = 1 },
            new Metadata { { "authorization", "Bearer " + token } }).ResponseAsync.ConfigureAwait(false);

        result.Count.Should().Be(2);
        result.Data.Count.Should().Be(1);
    }

    private static async Task CreateGreetingAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/greetings",
            new { name },
            JsonOptions).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
