// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Formatters;
using MessagePack;
using System.Threading.Tasks;
using System;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public class MessagePackOutputFormatter : OutputFormatter
    {
        const string ContentType = "application/x-msgpack";

        readonly IFormatterResolver _resolver;

        public MessagePackOutputFormatter()
            : this(null)
        {            
        }
        public MessagePackOutputFormatter(IFormatterResolver resolver)
        {
            SupportedMediaTypes.Add(ContentType);
            _resolver = resolver ?? MessagePackSerializer.DefaultResolver;
        }

        protected override bool CanWriteType(Type type)
        {
            return _resolver.GetFormatterDynamic(type) != null;
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
                    MessagePackSerializer.NonGeneric.Serialize(context.Object.GetType(), context.HttpContext.Response.Body, context.Object, _resolver);
                    return Task.CompletedTask;
                }
            }
            else
            {
                MessagePackSerializer.NonGeneric.Serialize(context.ObjectType, context.HttpContext.Response.Body, context.Object, _resolver);
                return Task.CompletedTask;
            }
        }
    }
}
