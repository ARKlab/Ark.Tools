using Ark.Tools.Core.EntityTag;
using Ark.Tools.Http;

using AwesomeAssertions;

using Flurl.Http;

using MessagePack;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;

using Reqnroll;

namespace WebApplicationDemo.Tests
{
    [Binding]
    public sealed class TestClient : IDisposable
    {
        private readonly IFlurlClient _client;
        private readonly IFormatterResolver _formatter;
        private readonly string _version;

        private IFlurlResponse? _backProperty;
        private IFlurlResponse _lastResponse
        {
            get => _backProperty ?? throw new InvalidOperationException("Make a request first");
            set { _backProperty?.Dispose(); _backProperty = value; }
        }

        public IFlurlResponse LastResponse => _lastResponse;

        public TestClient(FeatureContext fctx, ScenarioContext sctx, IFlurlClient client)
        {
            _client = client.WithHeader("Accept", "application/x-msgpack;q=1.0, application/json;q=0.9");

            _formatter = MessagePack.Resolvers.CompositeResolver.Create(
                MessagePack.NodaTime.NodatimeResolver.Instance,
                MessagePack.Resolvers.DynamicEnumAsStringResolver.Instance,
                MessagePack.Resolvers.StandardResolver.Instance
                );

            var tags = sctx.ScenarioInfo.Tags.Concat(fctx.FeatureInfo.Tags);

            _version = tags?.FirstOrDefault(x => x.StartsWith("Api:", StringComparison.Ordinal))?["Api:".Length..] ?? "v" + "1.0"/*CommonConstants.ApiVersions.Last()*/;
        }

        public HttpStatusCode GetLastStatusCode()
        {
            return (HttpStatusCode)_lastResponse.StatusCode;
        }

        public void Get(string requestUri, IEntityWithETag? e)
        {
            Get(requestUri, e != null ? new EntityTagHeaderValue($"\"{e._ETag}\"") : null);
        }

        public void Get(string requestUri, EntityTagHeaderValue? e)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (e != null)
                req.Headers.Add("If-None-Match", e.ToString());

            _lastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        public void Get(string[] requestUriParts, EntityTagHeaderValue? e)
        {
            var req = _client.Request([_version]);

            foreach (var part in requestUriParts)
                req.AppendPathSegment(part, true);

            if (e != null)
                req.Headers.Add("If-None-Match", e.ToString());

            _lastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        public void Get(string[] requestUriParts, string? etag = null)
        {
            Get(requestUriParts, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
        }

        public void Get(string requestUri, string? etag = null)
        {
            Get(requestUri, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
        }

        public void Delete(string requestUri)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            _lastResponse = req.DeleteAsync().GetAwaiter().GetResult();
        }

        public void PostAsJson(string requestUri) => PostAsJson<string>(requestUri, null);

        public void PostAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.Headers.Add("If-Match", $"\"{eTag._ETag}\"");

            _lastResponse = req.PostJsonAsync(body).GetAwaiter().GetResult();
        }

        public void PutAsJson(string requestUri) => PutAsJson<string>(requestUri, null);

        public void PutAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

            _lastResponse = req.PutJsonAsync(body).GetAwaiter().GetResult();
        }

        public void PatchAsJson(string requestUri) => PatchAsJson<string>(requestUri, null);

        public void PatchAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

            _lastResponse = req.PatchJsonAsync(body).GetAwaiter().GetResult();
        }

        public T ReadAsJson<T>()
        {
            if (_lastResponse == null)
                throw new InvalidOperationException("I suggest to make a request first ...");

            return _lastResponse.GetJsonAsync<T>().GetAwaiter().GetResult();
        }

        public T? ReadAsMsgPack<T>()
        {
            if (_lastResponse == null)
                throw new InvalidOperationException("I suggest to make a request first ...");

            return _lastResponse.GetMsgPackAsync<T>(_formatter).GetAwaiter().GetResult();
        }

        public string ReadAsString()
        {
            if (_lastResponse == null)
                throw new InvalidOperationException("I suggest to make a request first ...");

            return _lastResponse.GetStringAsync().GetAwaiter().GetResult();
        }

        [When(@"I get url (.*)")]
        public void WhenIGetUrl(string url)
        {
            var req = _client.Request(url);
            _lastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        [Then("The request succeded")]
        public void ThenTheRequestSucceded()
        {
            _lastResponse.ResponseMessage.Should().Be2XXSuccessful();
        }

        [Then(@"The request fails with (.*)")]
        public void ThenTheRequestFailsWith(HttpStatusCode code)
        {
            _lastResponse.ResponseMessage.Should().HaveHttpStatusCode(code);
        }

        [Then(@"The problem detail type contains (.*)")]
        public void ThenTheProblemDetailTypeContains(string expectedProblemDetailType)
        {
            var problemDetail = _lastResponse.GetJsonAsync<ProblemDetails>().GetAwaiter().GetResult();
            problemDetail?.Type?.Should().Contain(expectedProblemDetailType);
        }

        [Then(@"The request returns (.*)")]
        public void ThenTheRequestReturns(HttpStatusCode code)
        {
            _lastResponse.ResponseMessage.Should().HaveHttpStatusCode(code);
        }

        public void Dispose()
        {
            _lastResponse?.Dispose();
            _client?.Dispose();
        }
    }
}
