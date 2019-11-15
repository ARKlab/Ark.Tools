// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public class MessagePackInputFormatter : InputFormatter
    {
        const string ContentType = "application/x-msgpack";

        readonly IFormatterResolver _resolver;

        public MessagePackInputFormatter()
            : this(null)
        {
        }

        public MessagePackInputFormatter(IFormatterResolver resolver)
        {
            SupportedMediaTypes.Add(ContentType);
            _resolver = resolver ?? MessagePackSerializer.DefaultResolver;
        }

        protected override bool CanReadType(Type type)
        {
            return _resolver.GetFormatterDynamic(type) != null && base.CanReadType(type);
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            EnsureArg.IsNotNull(context);

            var request = context.HttpContext.Request;

            if (!request.Body.CanSeek)
            {
				request.EnableBuffering();

				await request.Body.DrainAsync(CancellationToken.None);
                request.Body.Seek(0L, SeekOrigin.Begin);
            }

            var result = MessagePackSerializer.NonGeneric.Deserialize(context.ModelType, request.Body, _resolver);

            return await InputFormatterResult.SuccessAsync(result);
        }
    }
}
