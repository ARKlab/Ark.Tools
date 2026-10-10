// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using MessagePack;
using MessagePack.Resolvers;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Verifies that MessagePack contracts carrying value objects round-trip through the sample resolver.</summary>
[TestClass]
public sealed class MessagePackContractTests
{
    /// <summary>A streamed book carries a Vogen title and a sensitive <see cref="PersonName"/> author.</summary>
    [TestMethod]
    public void BookStreamItemRoundTripsThroughTheSampleResolver()
    {
        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(SampleMessagePackFormatters.Resolver, StandardResolver.Instance));
        var item = new BookStreamItem { Index = 1, Title = BookTitle.From("Book 1"), Author = PersonName.From("Author 1") };

        var read = MessagePackSerializer.Deserialize<BookStreamItem>(MessagePackSerializer.Serialize(item, options), options);

        read.Should().Be(item);
    }
}
