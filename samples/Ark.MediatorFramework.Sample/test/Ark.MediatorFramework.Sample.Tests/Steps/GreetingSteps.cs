// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Tests.Auth;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using Reqnroll;

using AppComposeGreetingRequest = Ark.MediatorFramework.Sample.Application.ComposeGreetingRequest;
using AppComposeGreetingResponse = Ark.MediatorFramework.Sample.Application.ComposeGreetingResponse;
using AppCreateGreetingRequest = Ark.MediatorFramework.Sample.Application.CreateGreetingRequest;
using AppGreetingResponse = Ark.MediatorFramework.Sample.Application.GreetingResponse;
using AppGreetingResponseV2 = Ark.MediatorFramework.Sample.Application.GreetingResponseV2;
using AppAuditRecord = Ark.MediatorFramework.Sample.Application.AuditRecord;
using GrpcCreateGreetingRequest = Ark.MediatorFramework.Sample.GrpcClient.CreateGreetingRequest;
using GrpcGetGreetingQuery = Ark.MediatorFramework.Sample.GrpcClient.GetGreetingQuery;
using GrpcGreetingResponse = Ark.MediatorFramework.Sample.GrpcClient.GreetingResponse;
using GrpcGreetingsV1Client = Ark.MediatorFramework.Sample.GrpcClient.GreetingsV1.GreetingsV1Client;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines public-transport behavioral steps for the greeting sample.</summary>
[Binding]
public sealed class GreetingSteps
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    private readonly SampleTestContext _context;
    private readonly AuthTestContext _authContext;
    private AppGreetingResponse? _greeting;
    private GrpcGreetingResponse? _grpcGreeting;
    private AppGreetingResponseV2? _versionTwoGreeting;
    private HttpResponseMessage? _response;
    private StatusCode? _grpcErrorStatus;
    private AppAuditRecord? _audit;

    /// <summary>Initializes a new instance of the <see cref="GreetingSteps"/> class.</summary>
    /// <param name="context">The scenario's isolated sample host.</param>
    /// <param name="authContext">The scenario's authentication context.</param>
    public GreetingSteps(SampleTestContext context, AuthTestContext authContext)
    {
        _context = context;
        _authContext = authContext;
    }

    [Given(@"I create the greeting ""(.*)"" over HTTP")]
    [When(@"I create the greeting ""(.*)"" over HTTP")]
    public async Task WhenICreateTheGreetingOverHttp(string name)
    {
        _response = await _context.Client.PostAsJsonAsync(
            "/api/v1/greetings",
            new AppCreateGreetingRequest { Name = name },
            JsonOptions).ConfigureAwait(false);

        if (_response.IsSuccessStatusCode)
            _greeting = await _response.Content.ReadFromJsonAsync<AppGreetingResponse>(JsonOptions).ConfigureAwait(false);
    }

    [When(@"I create the greeting ""(.*)"" over gRPC")]
    public async Task WhenICreateTheGreetingOverGrpc(string name)
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = _context.Client,
        });
        try
        {
            var result = await new GrpcGreetingsV1Client(channel).CreateGreetingAsync(
                new GrpcCreateGreetingRequest { Name = name }).ResponseAsync.ConfigureAwait(false);
            _grpcGreeting = result;
        }
        catch (RpcException exception)
        {
            _grpcErrorStatus = exception.StatusCode;
        }
    }

    [When(@"I query the greeting through version two")]
    public async Task WhenIQueryTheGreetingThroughVersionTwo()
    {
        _greeting.Should().NotBeNull();
        _versionTwoGreeting = await _context.Client.GetFromJsonAsync<AppGreetingResponseV2>(
            $"/api/v2/greetings-v2/{_greeting!.Id}",
            JsonOptions).ConfigureAwait(false);
    }

    [When(@"I compose the greeting ""(.*)"" over HTTP")]
    public async Task WhenIComposeTheGreetingOverHttp(string name)
    {
        _response = await _context.Client.PostAsJsonAsync(
            "/api/v1/greetings/compose",
            new AppComposeGreetingRequest { Name = name },
            JsonOptions).ConfigureAwait(false);
        _response.EnsureSuccessStatusCode();
        var composition = await _response.Content.ReadFromJsonAsync<AppComposeGreetingResponse>(JsonOptions).ConfigureAwait(false);
        _greeting = new AppGreetingResponse
        {
            Id = composition!.Id,
            Message = string.Empty,
        };
    }

    [Then(@"the greeting is available over HTTP")]
    public async Task ThenTheGreetingIsAvailableOverHttp()
    {
        _greeting.Should().NotBeNull();
        var response = await _context.Client.GetAsync(
            new Uri($"/api/v1/greetings/{_greeting!.Id}", UriKind.Relative)).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var greeting = await response.Content.ReadFromJsonAsync<AppGreetingResponse>(JsonOptions).ConfigureAwait(false);
        greeting.Should().NotBeNull();
        greeting!.Id.Should().Be(_greeting.Id);
    }

    [Then(@"the greeting is available over gRPC")]
    public async Task ThenTheGreetingIsAvailableOverGrpc()
    {
        _grpcGreeting.Should().NotBeNull();
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = _context.Client,
        });
        var greeting = await new GrpcGreetingsV1Client(channel).GetGreetingAsync(
            new GrpcGetGreetingQuery { Id = _grpcGreeting!.Id }).ResponseAsync.ConfigureAwait(false);

        greeting.Id.Should().Equal(_grpcGreeting.Id);
        greeting.Message.Should().Be(_grpcGreeting.Message);
    }

    [Then(@"the request returns a business rule violation")]
    public void ThenTheRequestReturnsABusinessRuleViolation()
    {
        _response.Should().NotBeNull();
        _response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Then(@"the request returns validation errors")]
    public async Task ThenTheRequestReturnsValidationErrors()
    {
        _response.Should().NotBeNull();
        _response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problemDetails = await _response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        problemDetails.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name must not be empty.");
    }

    [Then(@"the gRPC request is invalid")]
    public void ThenTheGrpcRequestIsInvalid()
    {
        _grpcErrorStatus.Should().Be(StatusCode.InvalidArgument);
    }

    [Then(@"the request is unauthorized")]
    public void ThenTheRequestIsUnauthorized()
    {
        _response.Should().NotBeNull();
        _response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Then(@"the version two greeting includes its message length")]
    public void ThenTheVersionTwoGreetingIncludesItsMessageLength()
    {
        _versionTwoGreeting.Should().NotBeNull();
        _versionTwoGreeting!.MessageLength.Should().Be(_versionTwoGreeting.Message.Length);
    }

    [Then(@"the composed greeting is eventually available over HTTP")]
    public async Task ThenTheComposedGreetingIsEventuallyAvailableOverHttp()
    {
        _greeting.Should().NotBeNull();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        HttpResponseMessage? response;
        do
        {
            response = await _context.Client.GetAsync(
                new Uri($"/api/v1/greetings/{_greeting!.Id}", UriKind.Relative)).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                response.Dispose();
                return;
            }

            response.Dispose();
            await Task.Delay(50).ConfigureAwait(false);
        }
        while (DateTime.UtcNow < deadline);

        throw new TimeoutException("The composed greeting was not completed within 10 seconds.");
    }

    [Then(@"the audit query contains a (.*) operation for ""(.*)""")]
    public async Task ThenTheAuditQueryContainsRecordFor(string operation, string userId)
    {
        var response = await _context.Client.GetAsync(
            new Uri("/api/v1/audits?skip=0&limit=25", UriKind.Relative)).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue(body);
        var audits = JsonSerializer.Deserialize<Ark.Tools.Core.PagedResult<AppAuditRecord>>(body, JsonOptions);
        _audit = audits!.Data.Single(record => record.Operation == operation && record.UserId == userId);
        _audit.EntityType.Should().Be(typeof(AppGreetingResponse).Name);
        _audit.Identifier.Should().NotBeNullOrWhiteSpace();
        _audit.Timestamp.Should().NotBe(default);
    }
}
