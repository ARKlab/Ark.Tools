using CsvHelper;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.AspNetCore.AspNetCore
{
    public class CsvOutputFormatter : TextOutputFormatter
    {
        public CsvOutputFormatter()
        {
            SupportedMediaTypes.Add(Microsoft.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/csv"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanWriteType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return _isTypeOfIEnumerable(type);
        }

        private bool _isTypeOfIEnumerable(Type type) => typeof(IEnumerable).IsAssignableFrom(type);

        private Type _elementType(Type type)
        {
            // Type is Array
            // short-circuit if you expect lots of arrays 
            if (type.IsArray)
                return type.GetElementType();

            // type is IEnumerable<T>;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            // type implements/extends IEnumerable<T>;
            var enumType = type.GetInterfaces()
                                    .Where(t => t.IsGenericType &&
                                           t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                                    .Select(t => t.GenericTypeArguments[0]).FirstOrDefault();
            return enumType;
        }

        public async override Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding encoding)
        {
            var response = context.HttpContext.Response;
            var cultureFeature = context.HttpContext.Features.Get<IRequestCultureFeature>();
            var culture = cultureFeature?.RequestCulture?.Culture ?? CultureInfo.InvariantCulture;
            var elementType = _elementType(context.ObjectType) ?? context.ObjectType;

            using (var textWriter = context.WriterFactory(response.Body, encoding))
            using (var csv = new CsvWriter(textWriter))
            {
                csv.Configuration.AutoMap(elementType);
                csv.Configuration.SanitizeForInjection = true;
                csv.Configuration.CultureInfo = culture;

                if (context.Object is IEnumerable)
                {
                    csv.WriteRecords(context.Object as IEnumerable);
                }
                else
                {
                    var array = Array.CreateInstance(elementType, 1);
                    array.SetValue(context.Object, 0);
                    csv.WriteRecords(array);
                }
                await csv.FlushAsync();
                await textWriter.FlushAsync();
            }
        }
    }
}
