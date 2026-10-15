// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Flurl.Http.Testing;

using MessagePack;
using MessagePack.Resolvers;

namespace Ark.Tools.Http.Tests;

/// <summary>
/// Verifies that the MessagePack Flurl helpers round-trip payloads and reuse one formatter per resolver.
/// </summary>
[TestClass]
public sealed class MsgPackExTests
{
    private const string _url = "http://localhost/payload";

    private static readonly IFormatterResolver _resolver = ContractlessStandardResolver.Instance;

    /// <summary>Gets or sets the MSTest context.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Verifies that a MessagePack GET deserializes the response body.</summary>
    [TestMethod]
    public async Task GetMsgPackAsync_Response_Deserializes()
    {
        var expected = new Payload { Name = "Customer A", Value = 42 };
        var bytes = MessagePackSerializer.Serialize(expected, MessagePackSerializerOptions.Standard.WithResolver(_resolver), TestContext.CancellationToken);
        using var httpTest = new HttpTest();
        httpTest.RespondWith(() => new ByteArrayContent(bytes));

        var result = await _url.GetMsgPackAsync<Payload>(_resolver, cancellationToken: TestContext.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    /// <summary>Verifies that POST and PUT serialize the body and share one formatter for the same resolver.</summary>
    [TestMethod]
    public async Task PostAndPutMsgPackAsync_SameResolver_ReuseFormatter()
    {
        var payload = new Payload { Name = "Customer A", Value = 42 };
        using var httpTest = new HttpTest();

        using var post = await _url.PostMsgPackAsync(payload, _resolver, cancellationToken: TestContext.CancellationToken);
        using var put = await _url.PutMsgPackAsync(payload, _resolver, cancellationToken: TestContext.CancellationToken);

        var postContent = (ObjectContent)httpTest.CallLog[0].HttpRequestMessage.Content!;
        var putContent = (ObjectContent)httpTest.CallLog[1].HttpRequestMessage.Content!;
        putContent.Formatter.Should().BeSameAs(postContent.Formatter);

        var body = await postContent.ReadAsByteArrayAsync(TestContext.CancellationToken);
        MessagePackSerializer.Deserialize<Payload>(body, MessagePackSerializerOptions.Standard.WithResolver(_resolver), TestContext.CancellationToken)
            .Should().BeEquivalentTo(payload);
    }

    /// <summary>MessagePack test payload.</summary>
    public sealed class Payload
    {
        /// <summary>Gets or sets the name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the value.</summary>
        public int Value { get; set; }
    }
}
