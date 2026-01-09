// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;

using Microsoft.AspNetCore.Mvc.Formatters;


namespace Ark.Tools.AspNetCore.MessagePackFormatter;

public class MessagePackOutputFormatter : OutputFormatter
{
    const string _contentType = "application/x-msgpack";

    readonly MessagePackSerializerOptions _options;

    public MessagePackOutputFormatter()
        : this(MessagePackSerializer.DefaultOptions)
    {
    }

    public MessagePackOutputFormatter(IFormatterResolver resolver)
        : this(MessagePackSerializer.DefaultOptions.WithResolver(resolver))
    {
    }

    public MessagePackOutputFormatter(MessagePackSerializerOptions options)
    {
        SupportedMediaTypes.Add(_contentType);

        _options = options;
    }

    protected override bool CanWriteType(Type? type)
    {
        if (type == null) return false;
        return _options.Resolver.GetFormatterDynamic(type) != null;
    }

    public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {

        if (context.Object == null)
        {
            var writer = context.HttpContext.Response.BodyWriter;
            if (writer == null)
            {
                context.HttpContext.Response.Body.WriteByte(MessagePackCode.Nil);
                return Task.CompletedTask;
            }

            var span = writer.GetSpan(1);
            span[0] = MessagePackCode.Nil;
            writer.Advance(1);
            return writer.FlushAsync(context.HttpContext.RequestAborted).AsTask();
        }
        else
        {
            var objectType = context.ObjectType == null || context.ObjectType == typeof(object) ? context.Object.GetType() : context.ObjectType;

            var writer = context.HttpContext.Response.BodyWriter;
            if (writer == null)
            {
                return MessagePackSerializer.SerializeAsync(objectType, context.HttpContext.Response.Body, context.Object, _options, context.HttpContext.RequestAborted);
            }

            MessagePackSerializer.Serialize(objectType, writer, context.Object, _options, context.HttpContext.RequestAborted);
            return writer.FlushAsync(context.HttpContext.RequestAborted).AsTask();
        }
    }
}