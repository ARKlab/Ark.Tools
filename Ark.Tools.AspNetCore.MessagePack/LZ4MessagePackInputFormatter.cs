// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
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
    public class LZ4MessagePackInputFormatter : InputFormatter
    {
        const string ContentType = "application/x.msgpacklz4";

        readonly MessagePackSerializerOptions _options;

        public LZ4MessagePackInputFormatter()
            : this(null)
        {
        }

        public LZ4MessagePackInputFormatter(IFormatterResolver? resolver)
        {
            SupportedMediaTypes.Add(ContentType);

            if (resolver == null)
                _options = MessagePackSerializer.DefaultOptions;
            else
                _options = MessagePackSerializer.DefaultOptions.WithResolver(resolver);

            _options = _options.WithCompression(MessagePackCompression.Lz4Block);
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

            var result = await MessagePackSerializer.DeserializeAsync(context.ModelType, request.Body, _options, context.HttpContext.RequestAborted);

            return await InputFormatterResult.SuccessAsync(result);
        }
    }
}
