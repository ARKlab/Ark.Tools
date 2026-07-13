// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Net;
using System.Net.Http.Json;

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Grpc.Net.Client;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines public-transport behavioral steps for the greeting sample.</summary>
[Binding]
public sealed class GreetingSteps
{
    private readonly SampleTestContext _context;
    private GreetingResponse? _greeting;
    private GreetingResponseV2? _versionTwoGreeting;
    private HttpResponseMessage? _response;

    /// <summary>Initializes a new instance of the <see cref="GreetingSteps"/> class.</summary>
    /// <param name="context">The scenario's isolated sample host.</param>
    public GreetingSteps(SampleTestContext context)
    {
        _context = context;
    }

    [Given(@"I create the greeting ""(.*)"" over HTTP")]
    [When(@"I create the greeting ""(.*)"" over HTTP")]
    public async Task WhenICreateTheGreetingOverHttp(string name)
    {
        _response = await _context.Client.PostAsJsonAsync(
            "/api/v1/greetings",
            new CreateGreetingRequest { Name = name }).ConfigureAwait(false);

        if (_response.IsSuccessStatusCode)
            _greeting = await _response.Content.ReadFromJsonAsync<GreetingResponse>().ConfigureAwait(false);
    }

    [When(@"I create the greeting ""(.*)"" over gRPC")]
    public async Task WhenICreateTheGreetingOverGrpc(string name)
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _context.CreateGrpcHandler(),
        });
        var result = await new GreetingsV1.GreetingsV1Client(channel).CreateGreetingAsync(
            new CreateGreetingRequest { Name = name }).ResponseAsync.ConfigureAwait(false);

        _greeting = new GreetingResponse
        {
            Id = Guid.Parse(result.Id),
            Message = result.Message,
        };
    }

    [When(@"I query the greeting through version two")]
    public async Task WhenIQueryTheGreetingThroughVersionTwo()
    {
        _greeting.Should().NotBeNull();
        _versionTwoGreeting = await _context.Client.GetFromJsonAsync<GreetingResponseV2>(
            $"/api/v2/greetings-v2/{_greeting!.Id}").ConfigureAwait(false);
    }

    [When(@"I compose the greeting ""(.*)"" over HTTP")]
    public async Task WhenIComposeTheGreetingOverHttp(string name)
    {
        _response = await _context.Client.PostAsJsonAsync(
            "/api/v1/greetings/compose",
            new ComposeGreetingRequest { Name = name }).ConfigureAwait(false);
        _response.EnsureSuccessStatusCode();
        var composition = await _response.Content.ReadFromJsonAsync<ComposeGreetingResponse>().ConfigureAwait(false);
        _greeting = new GreetingResponse
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
        var greeting = await response.Content.ReadFromJsonAsync<GreetingResponse>().ConfigureAwait(false);
        greeting.Should().NotBeNull();
        greeting!.Id.Should().Be(_greeting.Id);
    }

    [Then(@"the request returns a business rule violation")]
    public void ThenTheRequestReturnsABusinessRuleViolation()
    {
        _response.Should().NotBeNull();
        _response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
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
}
