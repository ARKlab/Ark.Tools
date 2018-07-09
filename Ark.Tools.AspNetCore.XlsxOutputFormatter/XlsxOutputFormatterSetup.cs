using Ark.Tools.AspNetCore.XlsxOutputFormatter.Serialisation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter
{
    public class XlsxOutputFormatterSetup : IConfigureOptions<MvcOptions>
    {
        private readonly XlsxOutputFormatterOptions _options;
        private readonly Func<IEnumerable<IXlsxSerialiser>> _serializers;

        public XlsxOutputFormatterSetup(Func<IEnumerable<IXlsxSerialiser>> serializers, IOptions<XlsxOutputFormatterOptions> options)
        {
            _options = options.Value;
            _serializers = serializers;
        }
        
        public void Configure(MvcOptions opt)
        {

            opt.OutputFormatters.Add(new XlsxOutputFormatter(_options, _serializers));
            opt.FormatterMappings.SetMediaTypeMappingForFormat("xlsx", MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        }
    }
}
