// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;
using Flurl.Http;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static async Task<T> Receive<T>(this Task<HttpResponseMessage> response, MediaTypeFormatterCollection formatterCollection, CancellationToken cancellationToken = default)
        {
            var resp = await response.ConfigureAwait(false);
            if (resp == null) return default;

            return await resp.Content.ReadAsAsync<T>(formatterCollection, cancellationToken);
        }

        public static Task<T> GetAsync<T>(this IFlurlRequest request, MediaTypeFormatterCollection formatterCollection, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return request.SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<HttpResponseMessage> PostAsync<T>(this IFlurlRequest request, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return request.SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutAsync<T>(this IFlurlRequest request, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return request.SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }


        public static Task<T> GetAsync<T>(this Url url, MediaTypeFormatterCollection formatterCollection, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<HttpResponseMessage> PostAsync<T>(this Url url, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutAsync<T>(this Url url, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }


        public static Task<T> GetAsync<T>(this string url, MediaTypeFormatterCollection formatterCollection, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<HttpResponseMessage> PostAsync<T>(this string url, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutAsync<T>(this string url, T data, MediaTypeFormatter formatter, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, formatter);
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }

        public static IFlurlRequest WithAcceptHeader(this IFlurlRequest request, MediaTypeFormatterCollection formatters)
        {
            var cnt = formatters.Count;
            var step = 1.0 / (cnt+1);
            var sb = new StringBuilder();
            var headers = formatters.Select((x, i) => new MediaTypeWithQualityHeaderValue(x.SupportedMediaTypes.First().MediaType, 1 - (step * i)));

            return request.WithHeader("Accept", string.Join(",", headers));
        }
    }
}
