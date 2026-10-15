using System.Net;
using System.Net.Http.Formatting;

namespace Ark.Tools.Http;

public sealed class BufferedJsonMediaTypeFormatter : JsonMediaTypeFormatter
{
    public override async Task<object> ReadFromStreamAsync(Type type, Stream readStream, HttpContent content, IFormatterLogger formatterLogger, CancellationToken cancellationToken)
    {
        // A seekable stream is already buffered in memory (HttpCompletionOption.ResponseContentRead), so Newtonsoft's sync reads cannot block on the network.
        if (readStream.CanSeek)
            return await base.ReadFromStreamAsync(type, readStream, content, formatterLogger, cancellationToken).ConfigureAwait(false);

        var contentLength = content?.Headers.ContentLength ?? 0;
        var capacity = contentLength > 0 && contentLength <= Array.MaxLength ? (int)contentLength : 16 * 1024;

        using var buffer = new MemoryStream(capacity);
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