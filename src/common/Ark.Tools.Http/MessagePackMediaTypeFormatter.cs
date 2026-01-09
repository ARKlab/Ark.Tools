// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;

using System.Net;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;

namespace Ark.Tools.Http;

public class MessagePackMediaTypeFormatter : MediaTypeFormatter
{
    private readonly MessagePackSerializerOptions _options;

    public static MediaTypeHeaderValue DefaultMediaType { get; } = new MediaTypeHeaderValue("application/x-msgpack");

    public MessagePackMediaTypeFormatter()
        : this(MessagePackSerializer.DefaultOptions.Resolver)
    {
    }

    public MessagePackMediaTypeFormatter(IFormatterResolver resolver)
    {
        SupportedMediaTypes.Add(DefaultMediaType);

        if (resolver == null)
            _options = MessagePackSerializer.DefaultOptions;
        else
            _options = MessagePackSerializer.DefaultOptions.WithResolver(resolver);
    }

    public override bool CanReadType(Type type)
    {
        return _options.Resolver.GetFormatterDynamic(type) != null;
    }

    public override bool CanWriteType(Type type)
    {
        return _options.Resolver.GetFormatterDynamic(type) != null;
    }

    public override Task WriteToStreamAsync(Type type, object? value, Stream writeStream, HttpContent content, TransportContext transportContext)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        if (writeStream == null)
        {
            throw new ArgumentNullException(nameof(writeStream));
        }

        return MessagePackSerializer.SerializeAsync(type, writeStream, value, _options);
    }

    public override async Task<object?> ReadFromStreamAsync(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }
        if (readStream == null)
        {
            throw new ArgumentNullException(nameof(readStream));
        }

        return await MessagePackSerializer.DeserializeAsync(type, readStream, _options).ConfigureAwait(false);
    }
}