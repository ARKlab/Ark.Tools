// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.XlsxOutputFormatter.Attributes;
using Ark.Tools.AspNetCore.XlsxOutputFormatter.Serialisation;
using Ark.Tools.Core;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter
{
    public class XlsxOutputFormatter : OutputFormatter
    {
        /// <summary>
        /// Create a new ExcelMediaTypeFormatter.
        /// </summary>
        public XlsxOutputFormatter(XlsxOutputFormatterOptions options,
                                   Func<IEnumerable<IXlsxSerialiser>> serialisersFactory)
        {
            SupportedMediaTypes.Clear();
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.ms-excel"));

            Options = options;

            SerialisersFactory = serialisersFactory;
        }

        public XlsxOutputFormatterOptions Options { get; private set; }

        /// <summary>
        /// Non-default serialisers to be used by this formatter instance.
        /// </summary>
        public Func<IEnumerable<IXlsxSerialiser>> SerialisersFactory { get; set; }

        protected override bool CanWriteType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _isTypeOfIEnumerable(type);
        }

        private bool _isTypeOfIEnumerable(Type type) => typeof(IEnumerable).IsAssignableFrom(type);

        private Type? _elementType(Type? type)
        {
            if (type == null) return type;

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

        public override void WriteResponseHeaders(OutputFormatterWriteContext context)
        {
            base.WriteResponseHeaders(context);
            
            var response = context.HttpContext.Response;
            var elementType = _elementType(context.ObjectType) ?? context.ObjectType;
            string fileName;

            var excelDocumentAttribute = ReflectionHelper.GetAttribute<ExcelDocumentAttribute>(elementType);
            if (excelDocumentAttribute != null && !string.IsNullOrEmpty(excelDocumentAttribute.FileName))
            {
                // If attribute exists with file name defined, use that.
                fileName = excelDocumentAttribute.FileName;
            }
            else
            {
                fileName = elementType.Name;
            }

            if (!fileName.EndsWith("xlsx", StringComparison.CurrentCultureIgnoreCase)) fileName += ".xlsx";
            
            var cd = new ContentDispositionHeaderValue("attachment");
            cd.SetHttpFileName(fileName);
            response.Headers.Add(HeaderNames.ContentDisposition, cd.ToString());
        }

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var response = context.HttpContext.Response;
            var elementType = _elementType(context.ObjectType) ?? context.ObjectType;

            using (var document = new XlsxDocumentBuilder())
            {
                Options.CellStyle?.Invoke(document.Worksheet.Cells.Style);
                IEnumerable? data = context.Object as IEnumerable;
                if (data == null && context.Object != null)
                {
                    var array = Array.CreateInstance(elementType, 1);
                    array.SetValue(context.Object, 0);
                    data = array;
                }

                var serializer = SerialisersFactory().FirstOrDefault(x => x.CanSerialiseType(context.ObjectType, elementType));

                serializer?.Serialise(elementType, data, document);

                if (document.RowCount > 0)
                {
                    if (serializer?.IgnoreFormatting == true)
                    {
                        // Autofit cells if specified.
                        if (Options.AutoFit) document.AutoFit();
                    }
                    else FormatDocument(document);
                }
                using (var ms = new MemoryStream())
                {
                    await document.WriteToStream(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(response.Body);
                }
            }
        }

        /// <summary>
        /// Applies custom formatting to a document. (Used if matched serialiser supports formatting.)
        /// </summary>
        /// <param name="document">The <c>XlsxDocumentBuilder</c> wrapping the document to format.</param>
        private void FormatDocument(XlsxDocumentBuilder document)
        {
            // Header cell styles
            Options.HeaderStyle?.Invoke(document.Worksheet.Row(1).Style);
            if (Options.FreezeHeader) document.Worksheet.View.FreezePanes(2, 1);

            var cells = document.Worksheet.Cells[document.Worksheet.Dimension.Address];

            // Add autofilter and fit to max column width (if requested).
            cells.AutoFilter = Options.AutoFilter;
            if (Options.AutoFit) cells.AutoFitColumns();

            // Set header row where specified.
            if (Options.HeaderHeight.HasValue)
            {
                document.Worksheet.Row(1).Height = Options.HeaderHeight.Value;
                document.Worksheet.Row(1).CustomHeight = true;
            }
        }
    }
}
