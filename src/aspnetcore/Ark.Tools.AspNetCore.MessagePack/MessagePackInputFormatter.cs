// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;

using MessagePack;

using Microsoft.AspNetCore.Mvc.Formatters;

using System;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public class MessagePackInputFormatter : InputFormatter
    {
        const string ContentType = "application/x-msgpack";

        readonly MessagePackSerializerOptions _options;

        public MessagePackInputFormatter()
            : this(MessagePackSerializer.DefaultOptions)
        {
        }

        public MessagePackInputFormatter(IFormatterResolver resolver)
            : this(MessagePackSerializer.DefaultOptions.WithResolver(resolver))
        {
        }

        public MessagePackInputFormatter(MessagePackSerializerOptions options)
        {
            SupportedMediaTypes.Add(ContentType);

            _options = options;
        }

        protected override bool CanReadType(Type type)
        {
            return _options.Resolver.GetFormatterDynamic(type) != null && base.CanReadType(type);
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            EnsureArg.IsNotNull(context);

            var request = context.HttpContext.Request;
            var ctk = context.HttpContext.RequestAborted;

            var result = await MessagePackSerializer.DeserializeAsync(context.ModelType, request.Body, _options, ctk).ConfigureAwait(false);
            return await InputFormatterResult.SuccessAsync(result).ConfigureAwait(false);
        }
    }
}