using MessagePack;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Threading.Tasks;

namespace Ark.AspNetCore.MessagePackFormatter
{
    public class MessagePackInputFormatter : InputFormatter // , IApiRequestFormatMetadataProvider
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

        public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            var request = context.HttpContext.Request;
            var result = MessagePackSerializer.NonGeneric.Deserialize(context.ModelType, request.Body, _resolver);
            return InputFormatterResult.SuccessAsync(result);
        }
    }
}
