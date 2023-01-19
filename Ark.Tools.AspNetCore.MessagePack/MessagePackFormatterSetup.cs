// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    internal class MessagePackFormatterSetup : IConfigureOptions<MvcOptions>
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackFormatterSetup(IFormatterResolver resolver)
        {
            _resolver = resolver;
        }

        public void Configure(MvcOptions opt)
        {
            opt.FormatterMappings.SetMediaTypeMappingForFormat("mplz4", MediaTypeHeaderValue.Parse("application/x.msgpacklz4"));
            opt.FormatterMappings.SetMediaTypeMappingForFormat("mp", MediaTypeHeaderValue.Parse("application/x-msgpack"));

            opt.OutputFormatters.Add(new MessagePackOutputFormatter(_resolver));
            opt.InputFormatters.Add(new MessagePackInputFormatter(_resolver));

            opt.OutputFormatters.Add(new LZ4MessagePackOutputFormatter(_resolver));
            opt.InputFormatters.Add(new LZ4MessagePackInputFormatter(_resolver));
        }
    }
}
