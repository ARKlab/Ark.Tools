using Ark.Tools.Http;

using Flurl.Http;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Services;

public sealed class PostService : IPostService, IDisposable
{
    private readonly IFlurlClient _jsonPlaceHolderClient;

    private readonly Uri _url = new("https://jsonplaceholder.typicode.com/");

    public PostService(IArkFlurlClientFactory factory)
    {
        _jsonPlaceHolderClient = factory.Get(_url, s =>
        {
            s.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    public async Task<List<Post>> GetPosts(CancellationToken ctk)
    {
        var response = await _jsonPlaceHolderClient.Request("posts").GetStringAsync(cancellationToken: ctk).ConfigureAwait(false);

        var data = JsonSerializer.Deserialize<List<Post>>(response);

        return data ?? new List<Post>();
    }

    public void Dispose()
    {
        _jsonPlaceHolderClient?.Dispose();
    }
}