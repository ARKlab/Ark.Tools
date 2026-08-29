// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies transport-neutral logical names and native mappings.</summary>
[TestClass]
public sealed class MessagingLogicalNameTests
{
    [TestMethod]
    [DataRow("books-print_book.v1/events")]
    public void ValidLogicalNamesAcceptSupportedSeparators(string value)
    {
        Ark.Tools.MediatorFramework.MessagingLogicalName.IsValid(value).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("Books")]
    [DataRow("books//events")]
    [DataRow("/books")]
    [DataRow("books/")]
    [DataRow("books:events")]
    public void InvalidLogicalNamesAreRejected(string value)
    {
        Ark.Tools.MediatorFramework.MessagingLogicalName.IsValid(value).Should().BeFalse();
    }

    [TestMethod]
    public void InMemoryNamesRemainUnchanged()
    {
        var logical = "books-print_book.v1/events";
        MessagingEntityNameMapper.ToInMemory(logical).Should().Be(logical);
        MessagingEntityNameMapper.ToServiceBus(logical).Should().Be("books-print_book.v1-events");
        MessagingEntityNameMapper.ToStorageQueue(logical).Should().NotBe(logical);
    }

    [TestMethod]
    public void LongNamesUseStableHashSuffix()
    {
        var logical = new string('a', 300);
        var first = MessagingEntityNameMapper.ToServiceBus(logical);
        var second = MessagingEntityNameMapper.ToServiceBus(logical);
        first.Should().Be(second);
        first.Length.Should().Be(260);
        first.Should().Contain("-");
    }
}
