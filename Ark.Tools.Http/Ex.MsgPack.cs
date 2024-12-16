// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;
using Flurl.Http;
using MessagePack;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Mimiking Flurl signatures")]
    public static partial class Ex
    {
        public static async Task<T?> ReceiveMsgPack<T>(this Task<IFlurlResponse> response, IFormatterResolver formatterResolver)
        {
            var resp = await response;
            if (resp == null) return default;

            return await GetMsgPackAsync<T>(resp, formatterResolver);
        }

        public static async Task<T?> GetMsgPackAsync<T>(this IFlurlResponse response, IFormatterResolver formatterResolver)
        {
            using (var stream = await response.ResponseMessage.Content.ReadAsStreamAsync())
            {
                return await MessagePackSerializer.DeserializeAsync<T>(stream, MessagePackSerializerOptions.Standard.WithResolver(formatterResolver));
            }
        }

        public static Task<T?> GetMsgPackAsync<T>(this IFlurlRequest request, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return request.SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<IFlurlResponse> PostMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return request.SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return request.SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }

        public static Task<T?> GetMsgPackAsync<T>(this Url url, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<IFlurlResponse> PostMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }

        public static Task<T?> GetMsgPackAsync<T>(this string url, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
        }

        public static Task<IFlurlResponse> PostMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken);
        }

        public static Task<IFlurlResponse> PutMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
            var content = new ObjectContent<T>(data, new MessagePackMediaTypeFormatter(formatterResolver));
#pragma warning restore CA2000 // Dispose objects before losing scope
            return new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken);
        }
    }
}
