using Ark.Tools.Solid;

using Flurl.Http;
using Flurl.Http.Configuration;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using WebApplicationDemo.Api.Queries;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Queries
{
    public class Get_PostsQueryHandler : IQueryHandler<Get_PostsQuery.V1, List<Post>>
    {
        private readonly IFlurlClient _jsonPlaceHolderClient;
        private string _url = "https://jsonplaceholder.typicode.com/";

        public Get_PostsQueryHandler(IFlurlClientCache flurl)
        {
            _jsonPlaceHolderClient = flurl.GetOrAdd(typeof(Get_PostsQueryHandler).FullName, _url, builder =>
            {
                // customize client
                builder.WithTimeout(10);
            });
        }

        public List<Post> Execute(Get_PostsQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<List<Post>> ExecuteAsync(Get_PostsQuery.V1 query, CancellationToken ctk = default)
        {
            var response = await _jsonPlaceHolderClient.Request("posts").GetStringAsync(cancellationToken: ctk);

            var data = JsonSerializer.Deserialize<List<Post>>(response);

            return data ?? new List<Post> ();
        }
    }
}
