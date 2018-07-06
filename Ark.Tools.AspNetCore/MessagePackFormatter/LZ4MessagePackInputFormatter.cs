using MessagePack;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Threading.Tasks;

namespace Ark.AspNetCore.MessagePackFormatter
{
    public class LZ4MessagePackInputFormatter : InputFormatter // , IApiRequestFormatMetadataProvider
    {
        const string ContentType = "application/x.msgpacklz4";
        
        readonly IFormatterResolver _resolver;

        public LZ4MessagePackInputFormatter()
            : this(null)
        {
        }

        public LZ4MessagePackInputFormatter(IFormatterResolver resolver)
        {
            SupportedMediaTypes.Add(ContentType);
            _resolver = resolver ?? MessagePackSerializer.DefaultResolver;
        }

        protected override bool CanReadType(Type type)
        {
            return _resolver.GetFormatterDynamic(type) != null && base.CanReadType(type);
        }

        public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {            
            var request = context.HttpContext.Request;
            var result = LZ4MessagePackSerializer.NonGeneric.Deserialize(context.ModelType, request.Body, _resolver);
            return InputFormatterResult.SuccessAsync(result);
        }
    }
}
