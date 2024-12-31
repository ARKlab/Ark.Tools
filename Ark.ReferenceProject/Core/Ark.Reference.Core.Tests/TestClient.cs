using Ark.Tools.Core.EntityTag;

using Ark.Reference.Core.Common;
using Ark.Reference.Core.Tests.Auth;

using FluentAssertions;
using FluentAssertions.Execution;

using Flurl.Http;

using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;

using Reqnroll;

namespace Ark.Reference.Core.Tests
{
    [Binding]
    public sealed class TestClient
    {
        internal IFlurlClient _client;
        private readonly AuthTestContext _authContext;
        private readonly string _version;
        private IFlurlResponse? _backProperty;
        public IFlurlResponse LastResponse { get => _backProperty ?? throw new InvalidOperationException("LastResponse is null. Try making one first"); set { _backProperty?.Dispose(); _backProperty = value; } }
        private bool _isAuthenticated = true;

        private string _eTag = "";

        public TestClient(FeatureContext fctx, IFlurlClient client, AuthTestContext authContext)
        {
            _client = client;
            _authContext = authContext;
            var tags = fctx.FeatureInfo.Tags;

            _version = tags.FirstOrDefault(x => x.StartsWith("Api:", StringComparison.Ordinal))?.Substring("Api:".Length) ?? "v" + ApplicationConstants.Versions.First();
        }

        public void SetAuthorization(bool auth)
        {
            _isAuthenticated = auth;
        }

        public HttpStatusCode GetLastStatusCode()
        {
            return (HttpStatusCode)LastResponse.StatusCode;
        }

        public bool LastStatusCodeIsSuccess()
        {
            return LastResponse.ResponseMessage.IsSuccessStatusCode;
        }

        public void Get(string requestUri, IEntityWithETag? e)
        {
            Get(requestUri, e != null ? new EntityTagHeaderValue($"\"{e._ETag}\"") : null);
        }

        public void Get(string requestUri, EntityTagHeaderValue? e)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            _authContext.SetAuth(req);
            if (e != null)
                req.Headers.Add("If-None-Match", e.ToString());

            LastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        public void Get(string[] requestUriParts, EntityTagHeaderValue? e)
        {
            var req = _client.Request([_version]);

            foreach (var part in requestUriParts)
                req.AppendPathSegment(part, true);

            if (e != null)
                req.Headers.Add("If-None-Match", e.ToString());

            LastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        public void Get(string[] requestUriParts, string? etag = null)
        {
            Get(requestUriParts, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
        }

        public void Get(string requestUri, string? etag = null)
        {
            Get(requestUri, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
        }

        [When(@"I get versioned url (.*)")]
        public void Get(string requestUri)
        {
            Get(requestUri, (EntityTagHeaderValue?)null);
        }

        public void Delete(string requestUri)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            _authContext.SetAuth(req);

            LastResponse = req.DeleteAsync().GetAwaiter().GetResult();
        }

        public void PostAsJson(string requestUri) => PostAsJson(requestUri, (String?)null);

        public void PostAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            _authContext.SetAuth(req);
            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.Headers.Add("If-Match", $"\"{eTag._ETag}\"");
            else if (!string.IsNullOrEmpty(_eTag))
                req.Headers.Add("If-Match", $"\"{_eTag}\"");

            _eTag = "";
            LastResponse = req.PostJsonAsync(body).GetAwaiter().GetResult();
        }
        public void PostString(string requestUri, string? body)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (_isAuthenticated)
                _authContext.SetAuth(req);

            req.WithHeader("Content-Type", "application/xml");
            LastResponse = req.PostStringAsync(body).GetAwaiter().GetResult();
        }

        public void PostAsMultipart(string requestUri, FileData fileData)
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            if (_authContext.Token != null)
                req = req.WithOAuthBearerToken(_authContext.Token);

            var res = req.PostMultipartAsync(bc =>
            {
                bc.AddFile("file", fileData.Stream, fileData.FileName);
            });

            LastResponse = res.GetAwaiter().GetResult();
        }

        public void PostAsMultipart<T>(string requestUri, FileData fileData, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);

            if (_authContext.Token != null)
                req = req.WithOAuthBearerToken(_authContext.Token);

            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.Headers.Add("If-Match", $"\"{eTag._ETag}\"");

            LastResponse = req.PostMultipartAsync(bc =>
            {
                bc.AddFile("File", fileData.Stream, fileData.FileName);
                bc.AddJson("Create", body);
            }).GetAwaiter().GetResult();
        }

        public void PutAsJson(string requestUri) => PutAsJson(requestUri, (String?)null);

        public void PutAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            _authContext.SetAuth(req);
            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

            LastResponse = req.PutJsonAsync(body).GetAwaiter().GetResult();
        }

        public void PatchAsJson(string requestUri) => PatchAsJson(requestUri, (String?)null);

        public void PatchAsJson<T>(string requestUri, T? body = null) where T : class
        {
            var reqUriComposed = $"/{_version}/{requestUri}";

            var req = _client.Request(reqUriComposed);
            _authContext.SetAuth(req);
            if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
                req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

            LastResponse = req.PatchJsonAsync(body).GetAwaiter().GetResult();
        }

        public T ReadAs<T>()
        {
            return LastResponse.GetJsonAsync<T>().GetAwaiter().GetResult();
        }

        public string ReadResponseContent()
        {
            return LastResponse.GetStringAsync().GetAwaiter().GetResult();
        }

        public string ReadAsString()
        {
            return LastResponse.GetStringAsync().GetAwaiter().GetResult();
        }

        public byte[] ReadAsBytes()
        {
            return LastResponse.GetBytesAsync().GetAwaiter().GetResult();
        }

        [Then(@"the content type is '(.*)'")]
        public void ThenTheContentTypeIs(string expectedContentType)
        {
            var contentType = LastResponse.ResponseMessage
                .Content.Headers.GetValues("Content-Type").First();

            Assert.AreEqual(expectedContentType, contentType.Split(' ')[0]);
        }

        [When(@"I get url (.*)")]
        public void WhenIGetUrl(string url)
        {
            var req = _client.Request(url);
            LastResponse = req.GetAsync().GetAwaiter().GetResult();
        }

        [When(@"I use wrong eTag")]
        public void WhenIUseWrongETag()
        {
            _eTag = Guid.NewGuid().ToString();
        }

        [When(@"I use eTag '(.*)'")]
        public void WhenIUseETag(string eTag)
        {
            _eTag = eTag;
        }

        [Then("the request succeded")]
        public void ThenTheRequestSucceded()
        {
            if (!LastResponse.ResponseMessage.IsSuccessStatusCode)
            {
                using var s = new AssertionScope();
                var contents = LastResponse.GetStringAsync().GetAwaiter().GetResult();
                LastResponse.ResponseMessage.Should().BeSuccessful();
                contents.Should().Be(String.Empty); //hack to have error contents in test failure
            }
        }

        [Then(@"the request returns (.*)")]
        [Then(@"the request fails with (.*)")]
        public void ThenTheRequestFailsWith(HttpStatusCode code)
        {
            LastResponse.ResponseMessage.Should().HaveStatusCode(code);
        }

        [Then(@"the problem detail type contains (.*)")]
        public void ThenTheProblemDetailTypeContains(string expectedProblemDetailType)
        {
            var problemDetail = LastResponse.GetJsonAsync<ProblemDetails>().GetAwaiter().GetResult();
            problemDetail.Type.Should().Contain(expectedProblemDetailType);

        }

        [Given("I am an anonymous user")]
        public void SetUserAnon()
        {
            _isAuthenticated = false;
        }

        [Given("I am an authenticated user")]
        [BeforeScenario]
        public void SetUserAuthenticated()
        {
            _isAuthenticated = true;
        }

    }
}