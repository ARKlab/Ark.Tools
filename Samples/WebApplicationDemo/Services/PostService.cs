using Flurl.Http;
using System.Collections.Generic;
using WebApplicationDemo.Dto;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System;
using Ark.Tools.Http;

namespace WebApplicationDemo.Services
{
    public sealed class PostService : IPostService, IDisposable
    {
        private readonly IFlurlClient _jsonPlaceHolderClient;

        private string _url = "https://jsonplaceholder.typicode.com/";

        public PostService(IArkFlurlClientFactory factory)
        {
            _jsonPlaceHolderClient = factory.Get(_url, s =>
            {
                s.Timeout = TimeSpan.FromSeconds(10);
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
            _jsonPlaceHolderClient?.Dispose();
        }
    }
}
