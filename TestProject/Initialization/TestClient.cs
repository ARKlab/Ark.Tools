using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using TechTalk.SpecFlow;
using System;
using System.Net.Http.Headers;
using System.Linq;
using Ark.Tools.Core.EntityTag;
using Flurl.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestProject
{
	[Binding]
	public sealed class TestClient
	{
		internal IFlurlClient _client;

		private readonly string _version;
		private Task<HttpResponseMessage> _lastResponse;
		private static MediaTypeHeaderValue _jsonMediaType = new MediaTypeHeaderValue("application/json");

		public TestClient(FeatureContext fctx, ScenarioContext sctx, IFlurlClient client)
		{
			_client = client;

			var tags = sctx.ScenarioInfo.Tags.Concat(fctx.FeatureInfo.Tags);

			_version = tags.FirstOrDefault(x => x.StartsWith("Api:"))?.Substring("Api:".Length) ?? "v" + "1.0"/*CommonConstants.ApiVersions.Last()*/;
		}

		public HttpStatusCode GetLastStatusCode()
		{
			return _lastResponse.GetAwaiter().GetResult().StatusCode;
		}

		public void Get(string requestUri, IEntityWithETag e)
		{
			Get(requestUri, e != null ? new EntityTagHeaderValue($"\"{e?._ETag}\"") : null);
		}

		public void Get(string requestUri, EntityTagHeaderValue e)
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (e != null)
				req.Headers.Add("If-None-Match", e.ToString());

			_lastResponse = req.GetAsync();

			_lastResponse.GetAwaiter().GetResult();
		}

		public void Get(string[] requestUriParts, EntityTagHeaderValue e)
		{
			var req = _client.Request(new[] { _version });

			foreach (var part in requestUriParts)
				req.AppendPathSegment(part, true);

			if (e != null)
				req.Headers.Add("If-None-Match", e.ToString());

			_lastResponse = req.GetAsync();

			_lastResponse.GetAwaiter().GetResult();
		}

		public void Get(string[] requestUriParts, string etag = null)
		{
			Get(requestUriParts, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
		}

		public void Get(string requestUri, string etag = null)
		{
			Get(requestUri, etag != null ? new EntityTagHeaderValue($"\"{etag}\"") : null);
		}

		public void Delete(string requestUri)
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			_lastResponse = req.DeleteAsync();

			_lastResponse.GetAwaiter().GetResult();
		}

		public void PostAsJson(string requestUri) => PostAsJson(requestUri, (String)null);

		public void PostAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.Headers.Add("If-Match", $"\"{eTag._ETag}\"");

			_lastResponse = req.PostJsonAsync(body);

			_lastResponse.GetAwaiter().GetResult();
		}

		public void PutAsJson(string requestUri) => PutAsJson(requestUri, (String)null);

		public void PutAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

			_lastResponse = req.PutJsonAsync(body);

			_lastResponse.GetAwaiter().GetResult();
		}

		public void PatchAsJson(string requestUri) => PatchAsJson(requestUri, (String)null);

		public void PatchAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

			_lastResponse = req.PatchJsonAsync(body);

			_lastResponse.GetAwaiter().GetResult();
		}

		public T ReadAs<T>()
		{
			if (_lastResponse == null)
				throw new InvalidOperationException("I suggest to make a request first ...");

			return _lastResponse.ReceiveJson<T>().GetAwaiter().GetResult();
		}

		public string ReadAsString()
		{
			if (_lastResponse == null)
				throw new InvalidOperationException("I suggest to make a request first ...");

			return _lastResponse.ReceiveString().GetAwaiter().GetResult();
		}

		[When(@"I get url (.*)")]
		public void WhenIGetUrl(string url)
		{
			var req = _client.Request(url);
			_lastResponse = req.GetAsync();

			_lastResponse.GetAwaiter().GetResult();
		}

		[Then("The request succeded")]
		public void ThenTheRequestSucceded()
		{
			_lastResponse.GetAwaiter().GetResult().EnsureSuccessStatusCode();
		}

		[Then(@"The request fails with (.*)")]
		public void ThenTheRequestFailsWith(HttpStatusCode code)
		{
			Assert.AreEqual(code, _lastResponse.GetAwaiter().GetResult().StatusCode);
		}

		[Then(@"The problem detail type contains (.*)")]
		public void ThenTheProblemDetailTypeContains(string expectedProblemDetailType)
		{
			var problemDetail = _lastResponse.ReceiveJson<ProblemDetails>().GetAwaiter().GetResult();
			Assert.IsTrue(problemDetail.Type.Contains(expectedProblemDetailType));
		}

		[Then(@"The request returns (.*)")]
		public void ThenTheRequestReturns(HttpStatusCode code)
		{
			Assert.AreEqual(code, _lastResponse.GetAwaiter().GetResult().StatusCode);
		}
	}
}
