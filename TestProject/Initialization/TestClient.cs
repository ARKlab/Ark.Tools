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
	public sealed class TestClient : IDisposable
	{
		internal IFlurlClient _client;

		private readonly string _version;

		private IFlurlResponse _backProperty;
		private IFlurlResponse LastResponse { get => _backProperty;  set { _backProperty?.Dispose(); _backProperty = value; } }

		public TestClient(FeatureContext fctx, ScenarioContext sctx, IFlurlClient client)
		{
			_client = client;

			var tags = sctx.ScenarioInfo.Tags.Concat(fctx.FeatureInfo.Tags);

			_version = tags.FirstOrDefault(x => x.StartsWith("Api:"))?.Substring("Api:".Length) ?? "v" + "1.0"/*CommonConstants.ApiVersions.Last()*/;
		}

		public HttpStatusCode GetLastStatusCode()
		{
			return (HttpStatusCode)LastResponse.StatusCode;
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

			LastResponse = req.GetAsync().GetAwaiter().GetResult();
		}

		public void Get(string[] requestUriParts, EntityTagHeaderValue e)
		{
			var req = _client.Request(new[] { _version });

			foreach (var part in requestUriParts)
				req.AppendPathSegment(part, true);

			if (e != null)
				req.Headers.Add("If-None-Match", e.ToString());

			LastResponse = req.GetAsync().GetAwaiter().GetResult();
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

			LastResponse = req.DeleteAsync().GetAwaiter().GetResult();
		}

		public void PostAsJson(string requestUri) => PostAsJson(requestUri, (String)null);

		public void PostAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.Headers.Add("If-Match", $"\"{eTag._ETag}\"");

			LastResponse = req.PostJsonAsync(body).GetAwaiter().GetResult();
		}

		public void PutAsJson(string requestUri) => PutAsJson(requestUri, (String)null);

		public void PutAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

			LastResponse = req.PutJsonAsync(body).GetAwaiter().GetResult();
		}

		public void PatchAsJson(string requestUri) => PatchAsJson(requestUri, (String)null);

		public void PatchAsJson<T>(string requestUri, T body = null) where T : class
		{
			var reqUriComposed = $"/{_version}/{requestUri}";

			var req = _client.Request(reqUriComposed);

			if (body is IEntityWithETag eTag && !string.IsNullOrEmpty(eTag._ETag))
				req.WithHeader("If-Match", $"\"{eTag._ETag}\"");

			LastResponse = req.PatchJsonAsync(body).GetAwaiter().GetResult();
		}

		public T ReadAs<T>()
		{
			if (LastResponse == null)
				throw new InvalidOperationException("I suggest to make a request first ...");

			return LastResponse.GetJsonAsync<T>().GetAwaiter().GetResult();
		}

		public string ReadAsString()
		{
			if (LastResponse == null)
				throw new InvalidOperationException("I suggest to make a request first ...");

			return LastResponse.GetStringAsync().GetAwaiter().GetResult();
		}

		[When(@"I get url (.*)")]
		public void WhenIGetUrl(string url)
		{
			var req = _client.Request(url);
			LastResponse = req.GetAsync().GetAwaiter().GetResult();
		}

		[Then("The request succeded")]
		public void ThenTheRequestSucceded()
		{
			LastResponse.ResponseMessage.EnsureSuccessStatusCode();
		}

		[Then(@"The request fails with (.*)")]
		public void ThenTheRequestFailsWith(HttpStatusCode code)
		{
			Assert.AreEqual(code, LastResponse.StatusCode);
		}

		[Then(@"The problem detail type contains (.*)")]
		public void ThenTheProblemDetailTypeContains(string expectedProblemDetailType)
		{
			var problemDetail = LastResponse.GetJsonAsync<ProblemDetails>().GetAwaiter().GetResult();
			Assert.IsTrue(problemDetail.Type.Contains(expectedProblemDetailType));
		}

		[Then(@"The request returns (.*)")]
		public void ThenTheRequestReturns(HttpStatusCode code)
		{
			Assert.AreEqual(code, LastResponse.StatusCode);
		}

        public void Dispose()
        {
			LastResponse?.Dispose();
        }
    }
}
