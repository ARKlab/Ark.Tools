// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Formatters;
using MessagePack;
using System.Threading.Tasks;
using System;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public class LZ4MessagePackOutputFormatter : OutputFormatter
    {
        const string _contentType = "application/x.msgpacklz4";

        readonly MessagePackSerializerOptions _options;

        public LZ4MessagePackOutputFormatter()
            : this(null)
        {
        }

        public LZ4MessagePackOutputFormatter(IFormatterResolver? resolver)
        {
            SupportedMediaTypes.Add(_contentType);

            if (resolver == null)
                _options = MessagePackSerializer.DefaultOptions;
            else
                _options = MessagePackSerializer.DefaultOptions.WithResolver(resolver);

            _options = _options.WithCompression(MessagePackCompression.Lz4Block);
        }

        protected override bool CanWriteType(Type? type)
        {
            if (type == null) return false;
            return _options.Resolver.GetFormatterDynamic(type) != null;
        }

        public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            // 'object' want to use anonymous type serialize, etc...
            if (context.ObjectType == typeof(object))
            {
                if (context.Object == null)
                {
                    context.HttpContext.Response.Body.WriteByte(MessagePackCode.Nil);
                    return Task.CompletedTask;
                }
                else
                {
                    return MessagePackSerializer.SerializeAsync(context.Object.GetType(), context.HttpContext.Response.Body, context.Object, _options, context.HttpContext.RequestAborted);
                }
            }
            else
            {
                return MessagePackSerializer.SerializeAsync(context.ObjectType!, context.HttpContext.Response.Body, context.Object, _options, context.HttpContext.RequestAborted);
            }
        }
    }
}
