// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated Minimal API status and ProblemDetails behavior.</summary>
[TestClass]
public sealed class MinimalApiErrorsTests
{
    /// <summary>Verifies a configured success status is returned with the handler response.</summary>
    [TestMethod]
    public async Task ReturnsConfiguredSuccessStatusCode()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/status",
            new HostingStatusRequest { Value = "created" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("created");
    }

    /// <summary>Verifies a null handler result maps to the configured not-found status.</summary>
    [TestMethod]
    public async Task MapsNullResultToNotFound()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/not-found"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Verifies FluentValidation failures use the framework ProblemDetails mapping.</summary>
    [TestMethod]
    public async Task MapsValidationFailureToProblemDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/validation",
            new HostingValidationRequest { Value = "invalid" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var problem = await _readProblemAsync(response, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Title.Should().Be("Validation failed");
        ((JsonElement)problem.Extensions["errors"]!).GetProperty("Value")[0].GetString()
            .Should().Be("The synthetic value is invalid.");
    }

    /// <summary>Verifies business-rule failures preserve their status and payload.</summary>
    [TestMethod]
    public async Task MapsBusinessViolationToProblemDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/business-violation",
            new HostingBusinessViolationRequest { Value = "invalid" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var problem = await _readProblemAsync(response, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be((HttpStatusCode)422);
        problem.Title.Should().Be("Synthetic rule");
        problem.Detail.Should().Be("The synthetic business rule was violated.");
        ((JsonElement)problem.Extensions["businessRuleViolation"]!).GetProperty("type").GetString()
            .Should().Be("BusinessRuleViolation");
    }

    /// <summary>Verifies unexpected exceptions map to an internal-server ProblemDetails response.</summary>
    [TestMethod]
    public async Task MapsUnexpectedExceptionToProblemDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/unexpected",
            new HostingUnexpectedRequest { Value = "failure" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var problem = await _readProblemAsync(response, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        problem.Status.Should().Be(500);
        var payload = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        payload.Should().NotContain("The synthetic handler failed unexpectedly.");
        payload.Should().NotContain("InvalidOperationException");
    }

    /// <summary>Verifies a policy failure maps to a 403 ProblemDetails without leaked internals.</summary>
    [TestMethod]
    public async Task MapsForbiddenToProblemDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "authenticated");

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/authorized"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var problem = await _readProblemAsync(response, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Status.Should().Be(403);
        var payload = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        payload.Should().NotContain("Exception");
    }

    /// <summary>Verifies a malformed MessagePack body maps to a 400 ProblemDetails response.</summary>
    [TestMethod]
    public async Task MapsMalformedMessagePackBodyToProblemDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = new ByteArrayContent([0xC1, 0xFF, 0x00]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-msgpack");

        using var response = await client.PostAsync(
            new Uri("http://localhost/api/v1/hosting/requests/1"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var problem = await _readProblemAsync(response, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Title.Should().Be("INVALID_REQUEST_BODY");
        problem.Detail.Should().Be("Request body is missing or could not be deserialized.");
    }

    private static async Task<ProblemDetails> _readProblemAsync(
        HttpResponseMessage response,
        CancellationToken ctk)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ctk).ConfigureAwait(false),
            cancellationToken: ctk).ConfigureAwait(false);
        var problem = document.RootElement.Deserialize<ProblemDetails>();
        problem.Should().NotBeNull();
        return problem!;
    }
}
