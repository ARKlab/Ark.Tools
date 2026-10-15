// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Http;

using AwesomeAssertions;

using Flurl.Http;

using Reqnroll;

using System.Net;
using System.Text.Json.Serialization;

namespace WebApplicationDemo.Tests;

public sealed record JsonStreamPayload(string Name, int Count);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(JsonStreamPayload))]
internal sealed partial class JsonStreamContext : JsonSerializerContext
{
}

[Binding]
public sealed class JsonStreamSteps : IDisposable
{
    private readonly CapturingHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly FlurlClient _client;

    public JsonStreamSteps()
    {
        _httpClient = new HttpClient(_handler);
        _client = new FlurlClient(_httpClient);
    }

    [When("I post a JSON stream with Name {string} and Count {int}")]
    public async Task WhenIPostAJsonStreamWithNameAndCount(string name, int count)
    {
        using var response = await _client.Request("https://example.test/items")
            .PostJsonStreamAsync(new JsonStreamPayload(name, count), JsonStreamContext.Default.JsonStreamPayload)
            .ConfigureAwait(false);
    }

    [Then("the captured request is a POST with Content-Type (.*)")]
    public void ThenTheCapturedRequestIsAPostWithContentType(string contentType)
    {
        _handler.Method.Should().Be(HttpMethod.Post);
        _handler.ContentType.Should().Be(contentType);
    }

    [Then("the captured request has no precomputed Content-Length")]
    public void ThenTheCapturedRequestHasNoPrecomputedContentLength()
    {
        // A null length proves the body was not buffered before sending
        _handler.ContentLength.Should().BeNull();
    }

    [Then("the captured body is (.*)")]
    public void ThenTheCapturedBodyIs(string body)
    {
        _handler.Body.Should().Be(body);
    }

    public void Dispose()
    {
        _client.Dispose();
        _httpClient.Dispose();
        _handler.Dispose();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? ContentType { get; private set; }
        public long? ContentLength { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            ContentLength = request.Content?.Headers.ContentLength;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
