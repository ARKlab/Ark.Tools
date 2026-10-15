// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.Http.Tests;

/// <summary>
/// Verifies that <see cref="BufferedJsonMediaTypeFormatter"/> deserializes identically whether the
/// response body is already buffered (seekable) or still a network-like (non-seekable) stream.
/// </summary>
[TestClass]
public sealed class BufferedJsonMediaTypeFormatterTests
{
    private sealed record Payload(string Name, int Value);

    private static readonly byte[] _json = Encoding.UTF8.GetBytes("""{"Name":"Customer A","Value":42}""");

    /// <summary>Verifies that a buffered (seekable) body is deserialized.</summary>
    [TestMethod]
    public async Task ReadFromStreamAsync_SeekableStream_Deserializes()
    {
        var formatter = new BufferedJsonMediaTypeFormatter();
        using var content = new ByteArrayContent(_json);
        await using var stream = new MemoryStream(_json);

        var result = await formatter.ReadFromStreamAsync(typeof(Payload), stream, content, null!, TestContext.CancellationToken);

        result.Should().Be(new Payload("Customer A", 42));
    }

    /// <summary>Verifies that a non-seekable body is buffered and deserialized, with and without Content-Length.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ReadFromStreamAsync_NonSeekableStream_Deserializes(bool withContentLength)
    {
        var formatter = new BufferedJsonMediaTypeFormatter();
        using var content = new ByteArrayContent(_json);
        content.Headers.ContentLength = withContentLength ? _json.Length : null;
        await using var stream = new NonSeekableStream(new MemoryStream(_json));

        var result = await formatter.ReadFromStreamAsync(typeof(Payload), stream, content, null!, TestContext.CancellationToken);

        result.Should().Be(new Payload("Customer A", 42));
    }

    /// <summary>Verifies that a huge advertised Content-Length does not presize the buffer to that length.</summary>
    [TestMethod]
    public async Task ReadFromStreamAsync_HugeContentLength_DoesNotAllocateAdvertisedSize()
    {
        var formatter = new BufferedJsonMediaTypeFormatter();
        using var content = new ByteArrayContent(_json);
        content.Headers.ContentLength = Array.MaxLength;
        await using var stream = new NonSeekableStream(new MemoryStream(_json));
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        var result = await formatter.ReadFromStreamAsync(typeof(Payload), stream, content, null!, TestContext.CancellationToken);

        result.Should().Be(new Payload("Customer A", 42));
        (GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore).Should().BeLessThan(Array.MaxLength / 2);
    }

    /// <summary>Gets or sets the MSTest context.</summary>
    public TestContext TestContext { get; set; } = null!;

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
