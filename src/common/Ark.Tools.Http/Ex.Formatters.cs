// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;
using Flurl.Http;

using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static async Task<T?> Receive<T>(this Task<IFlurlResponse> response, MediaTypeFormatterCollection formatterCollection, CancellationToken cancellationToken = default)
        {
            var resp = await response.ConfigureAwait(false);
            if (resp == null) return default;

            return await resp.ResponseMessage.Content.ReadAsAsync<T>(formatterCollection, cancellationToken).ConfigureAwait(false);
        }

        public static Task<T?> GetAsync<T>(this IFlurlRequest request, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return request.SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<IFlurlResponse> PostAsync<T>(this IFlurlRequest request, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return request.SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutAsync<T>(this IFlurlRequest request, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return request.SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PatchAsync<T>(this IFlurlRequest request, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return request.SendAsync(new HttpMethod("PATCH"), content, completionOption, cancellationToken);
        }

        public static Task<T?> DeleteAsync<T>(this IFlurlRequest request, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return request.SendAsync(HttpMethod.Delete, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }


        public static Task<T?> GetAsync<T>(this Url url, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<IFlurlResponse> PostAsync<T>(this Url url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutAsync<T>(this Url url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PatchAsync<T>(this Url url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(new HttpMethod("PATCH"), content, completionOption, cancellationToken);
        }

        public static Task<T?> DeleteAsync<T>(this Url url, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Delete, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }



        public static Task<T?> GetAsync<T>(this string url, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }

        public static Task<IFlurlResponse> PostAsync<T>(this string url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutAsync<T>(this string url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PatchAsync<T>(this string url, T data, MediaTypeFormatter formatter, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, formatter);
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(new HttpMethod("PATCH"), content, completionOption, cancellationToken);
        }

        public static Task<T?> DeleteAsync<T>(this string url, MediaTypeFormatterCollection formatterCollection, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Delete, null, completionOption, cancellationToken).Receive<T>(formatterCollection, cancellationToken);
        }

        public static IFlurlRequest WithAcceptHeader(this IFlurlRequest request, MediaTypeFormatterCollection formatters)
        {
            var cnt = formatters.Count;
            var step = 1.0 / (cnt + 1);
            var headers = formatters.Select((x, i) => new MediaTypeWithQualityHeaderValue(x.SupportedMediaTypes.First().MediaType!, 1 - (step * i)));

            return request.WithHeader("Accept", string.Join(",", headers));
        }
    }
}