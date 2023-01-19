// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    public class LZ4MessagePackMediaTypeFormatter : MediaTypeFormatter
    {
        private MessagePackSerializerOptions _options;

        public static MediaTypeHeaderValue DefaultMediaType { get; } = new MediaTypeHeaderValue("application/x.msgpacklz4");

        public LZ4MessagePackMediaTypeFormatter()
            : this(MessagePackSerializer.DefaultOptions.Resolver)
        {
        }

        public LZ4MessagePackMediaTypeFormatter(IFormatterResolver resolver)
        {
            SupportedMediaTypes.Add(DefaultMediaType);

            if (resolver == null)
                _options = MessagePackSerializer.DefaultOptions;
            else
                _options = MessagePackSerializer.DefaultOptions.WithResolver(resolver);

            _options = _options.WithCompression(MessagePackCompression.Lz4Block);
        }

        public override bool CanReadType(Type type)
        {
            return _options.Resolver.GetFormatterDynamic(type) != null;
        }

        public override bool CanWriteType(Type type)
        {
            return _options.Resolver.GetFormatterDynamic(type) != null;
        }

        public override Task WriteToStreamAsync(Type type, object value, Stream writeStream, HttpContent content, TransportContext transportContext)
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

        public override async Task<object> ReadFromStreamAsync(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (readStream == null)
            {
                throw new ArgumentNullException(nameof(readStream));
            }

            return await MessagePackSerializer.DeserializeAsync(type, readStream, _options);
        }
    }
}
