// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Formatters;
using MessagePack;
using System.Threading.Tasks;
using System;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public class LZ4MessagePackOutputFormatter : OutputFormatter
    {
        const string ContentType = "application/x.msgpacklz4";
        
        readonly IFormatterResolver _resolver;

        public LZ4MessagePackOutputFormatter()
            : this(null)
        {
        }

        public LZ4MessagePackOutputFormatter(IFormatterResolver resolver)
        {
            SupportedMediaTypes.Add(ContentType);
            this._resolver = resolver ?? MessagePackSerializer.DefaultResolver;
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
                    LZ4MessagePackSerializer.NonGeneric.Serialize(context.Object.GetType(), context.HttpContext.Response.Body, context.Object, _resolver);
                    return Task.CompletedTask;
                }
            }
            else
            {
                LZ4MessagePackSerializer.NonGeneric.Serialize(context.ObjectType, context.HttpContext.Response.Body, context.Object, _resolver);
                return Task.CompletedTask;
            }
        }
    }
}
