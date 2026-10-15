// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;

using MessagePack;

using System.Runtime.CompilerServices;


namespace Ark.Tools.Http;


public static partial class Ex
{
    // Keyed weakly on the resolver so cached entries do not keep caller-owned resolvers alive.
    private static readonly ConditionalWeakTable<IFormatterResolver, MessagePackSerializerOptions> _msgPackOptions = new();
    // Keyed on the DefaultOptions snapshot too, because the formatter bakes DefaultOptions in and callers may replace them at runtime.
    private static readonly ConditionalWeakTable<MessagePackSerializerOptions, ConditionalWeakTable<IFormatterResolver, MessagePackMediaTypeFormatter>> _msgPackFormatters = new();

    private static MessagePackSerializerOptions _getMsgPackOptions(IFormatterResolver formatterResolver)
    {
        return _msgPackOptions.GetValue(formatterResolver, static r => MessagePackSerializerOptions.Standard.WithResolver(r));
    }

    private static MessagePackMediaTypeFormatter _getMsgPackFormatter(IFormatterResolver formatterResolver)
    {
        var defaults = MessagePackSerializer.DefaultOptions;
        return _msgPackFormatters.GetValue(defaults, static _ => new())
            .GetValue(formatterResolver, r => new MessagePackMediaTypeFormatter(defaults.WithResolver(r)));
    }

    public static async Task<T?> ReceiveMsgPack<T>(this Task<IFlurlResponse> response, IFormatterResolver formatterResolver)
    {
        var resp = await response.ConfigureAwait(false);
        if (resp == null) return default;

        return await GetMsgPackAsync<T>(resp, formatterResolver).ConfigureAwait(false);
    }

    public static async Task<T?> GetMsgPackAsync<T>(this IFlurlResponse response, IFormatterResolver formatterResolver)
    {
        var stream = await response.ResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await MessagePackSerializer.DeserializeAsync<T>(stream, _getMsgPackOptions(formatterResolver)).ConfigureAwait(false);
        }
    }

    public static Task<T?> GetMsgPackAsync<T>(this IFlurlRequest request, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        return request.SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
    }

    public static async Task<IFlurlResponse> PostMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await request.SendAsync(HttpMethod.Post, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IFlurlResponse> PutMsgPackAsync<T>(this IFlurlRequest request, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await request.SendAsync(HttpMethod.Put, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    public static Task<T?> GetMsgPackAsync<T>(this Url url, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
    }

    public static async Task<IFlurlResponse> PostMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IFlurlResponse> PutMsgPackAsync<T>(this Url url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    public static Task<T?> GetMsgPackAsync<T>(this string url, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        return new FlurlRequest(url).SendAsync(HttpMethod.Get, null, completionOption, cancellationToken).ReceiveMsgPack<T>(formatterResolver);
    }

    public static async Task<IFlurlResponse> PostMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await new FlurlRequest(url).SendAsync(HttpMethod.Post, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IFlurlResponse> PutMsgPackAsync<T>(this string url, T data, IFormatterResolver formatterResolver, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        using var content = new ObjectContent<T>(data, _getMsgPackFormatter(formatterResolver));
        return await new FlurlRequest(url).SendAsync(HttpMethod.Put, content, completionOption, cancellationToken).ConfigureAwait(false);
    }
}