// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;
using Flurl.Http;
using MessagePack;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static async Task<T> ReceiveMsgPack<T>(this Task<HttpResponseMessage> response, IFormatterResolver formatterResolver)
        {
            var resp = await response.ConfigureAwait(false);
            if (resp == null) return default;

            using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return await MessagePackSerializer.DeserializeAsync<T>(stream);
            }
        }

        public static Task<T> GetMsgPackAsync<T>(this IFlurlRequest request, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return request.SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<HttpResponseMessage> PostMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return request.SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return request.SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }

        public static Task<T> GetMsgPackAsync<T>(this Url url, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<HttpResponseMessage> PostMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }

        public static Task<T> GetMsgPackAsync<T>(this string url, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, cancellationToken, completionOption).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<HttpResponseMessage> PostMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, cancellationToken, completionOption);
        }

        public static Task<HttpResponseMessage> PutMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, CancellationToken cancellationToken = default, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, cancellationToken, completionOption);
        }
    }
}
