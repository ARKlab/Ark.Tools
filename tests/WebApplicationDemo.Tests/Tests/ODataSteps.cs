// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using AwesomeAssertions;

using Flurl.Http;

using Reqnroll;

using System.Text.Json;

namespace WebApplicationDemo.Tests;

/// <summary>
/// Step definitions for OData endpoint tests.
/// </summary>
[Binding]
public sealed class ODataSteps : IDisposable
{
#pragma warning disable CA2213 // _client lifetime is managed by the Reqnroll scenario container
    private readonly IFlurlClient _client;
#pragma warning restore CA2213
    private IFlurlResponse? _lastResponse;
    private JsonDocument? _lastDocument;

    public ODataSteps(ScenarioContext sctx)
    {
        _client = sctx.ScenarioContainer.Resolve<IFlurlClient>();
    }

    [When(@"I get OData url (.*)")]
    public async Task WhenIGetODataUrl(string url)
    {
        _lastResponse?.Dispose();
        _lastDocument?.Dispose();
        _lastDocument = null;

        _lastResponse = await _client.Request(url).GetAsync().ConfigureAwait(false);
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        if (_lastDocument != null) return _lastDocument;
        if (_lastResponse == null) throw new InvalidOperationException("Make a request first");

        var json = await _lastResponse.GetStringAsync().ConfigureAwait(false);
        _lastDocument = JsonDocument.Parse(json);
        return _lastDocument;
    }

    [Then(@"The OData request succeded")]
    public void ThenODataRequestSucceded()
    {
        if (_lastResponse == null) throw new InvalidOperationException("Make a request first");
        _lastResponse.ResponseMessage.Should().Be2XXSuccessful();
    }

    [Then(@"the OData response has value array")]
    public async Task ThenODataResponseHasValueArray()
    {
        var doc = await GetDocumentAsync().ConfigureAwait(false);
        doc.RootElement.TryGetProperty("value", out var value).Should().BeTrue("OData response must have a 'value' property");
        value.ValueKind.Should().Be(JsonValueKind.Array, "OData value must be an array");
    }

    [Then(@"the OData response value count is (\d+)")]
    public async Task ThenODataResponseValueCountIs(int count)
    {
        var doc = await GetDocumentAsync().ConfigureAwait(false);
        doc.RootElement.TryGetProperty("value", out var value).Should().BeTrue();
        value.GetArrayLength().Should().Be(count, $"OData value array should have {count} items");
    }

    [Then(@"the OData response first item has field (.*)")]
    public async Task ThenODataResponseFirstItemHasField(string fieldName)
    {
        var doc = await GetDocumentAsync().ConfigureAwait(false);
        var arr = doc.RootElement.GetProperty("value");
        arr.GetArrayLength().Should().BeGreaterThan(0, "Array must not be empty");
        var first = arr[0];
        first.TryGetProperty(fieldName, out _).Should().BeTrue($"First item should have field '{fieldName}'");
    }

    [Then(@"the OData response first item does not have field (.*)")]
    public async Task ThenODataResponseFirstItemDoesNotHaveField(string fieldName)
    {
        var doc = await GetDocumentAsync().ConfigureAwait(false);
        var arr = doc.RootElement.GetProperty("value");
        arr.GetArrayLength().Should().BeGreaterThan(0, "Array must not be empty");
        var first = arr[0];
        first.TryGetProperty(fieldName, out _).Should().BeFalse($"First item should not have field '{fieldName}'");
    }

    [Then(@"the OData single result has id equal to (\d+)")]
    public async Task ThenODataSingleResultHasId(int expectedId)
    {
        var doc = await GetDocumentAsync().ConfigureAwait(false);
        // OData single result for an entity is a direct object (not wrapped in value array)
        // Property names are camelCase in JSON
        int actualId;
        if (doc.RootElement.TryGetProperty("value", out var valueArr))
        {
            // Some OData implementations wrap in value array
            valueArr.GetArrayLength().Should().Be(1);
            actualId = valueArr[0].GetProperty("id").GetInt32();
        }
        else
        {
            actualId = doc.RootElement.GetProperty("id").GetInt32();
        }

        actualId.Should().Be(expectedId);
    }

    public void Dispose()
    {
        _lastDocument?.Dispose();
        _lastResponse?.Dispose();
    }
}
