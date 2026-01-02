using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{
    public class BufferedJsonMediaTypeFormatter : JsonMediaTypeFormatter
    {
        public override async Task<object> ReadFromStreamAsync(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream(16 * 1024);
            await readStream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);

            buffer.Seek(0, SeekOrigin.Begin);

            return await base.ReadFromStreamAsync(type, buffer, content, formatterLogger, cancellationToken).ConfigureAwait(false);
        }

        public override async Task WriteToStreamAsync(Type type, object value, Stream writeStream, HttpContent content, TransportContext transportContext, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream(16 * 1024);
            await base.WriteToStreamAsync(type, value, buffer, content, transportContext, cancellationToken).ConfigureAwait(false);

            buffer.Seek(0, SeekOrigin.Begin);

            await buffer.CopyToAsync(writeStream, 81920, cancellationToken).ConfigureAwait(false);
        }
    }
}