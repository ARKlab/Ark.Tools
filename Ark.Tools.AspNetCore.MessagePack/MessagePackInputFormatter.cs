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

        readonly MessagePackSerializerOptions _options;

        public MessagePackInputFormatter()
            : this(null)
        {
        }

        public MessagePackInputFormatter(IFormatterResolver? resolver)
        {
            SupportedMediaTypes.Add(ContentType);

            if (resolver == null)
                _options = MessagePackSerializer.DefaultOptions;
            else
                _options = MessagePackSerializer.DefaultOptions.WithResolver(resolver);
        }

        protected override bool CanReadType(Type type)
        {
            return _options.Resolver.GetFormatterDynamic(type) != null && base.CanReadType(type);
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

            var result = MessagePackSerializer.Deserialize(context.ModelType, request.Body, _options);

            return await InputFormatterResult.SuccessAsync(result);
        }
    }
}
