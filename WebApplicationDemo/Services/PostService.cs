using Flurl.Http.Configuration;
using Flurl.Http;
using System.Collections.Generic;

using WebApplicationDemo.Application.Handlers.Queries;
using WebApplicationDemo.Dto;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System;

namespace WebApplicationDemo.Services
{
    public sealed class PostService : IPostService, IDisposable
    {
        private readonly IFlurlClientCache _clientCache;
        private readonly IFlurlClient _jsonPlaceHolderClient;

        private string _url = "https://jsonplaceholder.typicode.com/";

        public PostService(IFlurlClientCache clientCache)
        {
            _clientCache = clientCache;

            _jsonPlaceHolderClient = _clientCache.GetOrAdd(typeof(PostService).FullName, _url, builder =>
            {
                // customize client
                builder.WithTimeout(10);
            });
        }

        public async Task<List<Post>> GetPosts(CancellationToken ctk)
        {
            var response = await _jsonPlaceHolderClient.Request("posts").GetStringAsync(cancellationToken: ctk);

            var data = JsonSerializer.Deserialize<List<Post>>(response);

            return data ?? new List<Post>();
        }

        public void Dispose()
        {
            _clientCache.Remove(typeof(PostService).FullName);
            _jsonPlaceHolderClient?.Dispose();
        }
    }
}
