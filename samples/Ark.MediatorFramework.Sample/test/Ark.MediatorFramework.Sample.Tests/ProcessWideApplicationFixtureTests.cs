// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Demonstrates the separately serialized process-wide application fixture.</summary>
[TestClass]
[DoNotParallelize]
[TestCategory("process-wide-fixture")]
public sealed class ProcessWideApplicationFixtureTests
{
    private static ProcessWideApplicationTestFixture _fixture = null!;

    /// <summary>Creates the process-wide fixture once for this separately serialized class.</summary>
    /// <param name="testContext">The MSTest context.</param>
    [ClassInitialize]
    public static void Initialize(TestContext testContext)
    {
        _ = testContext;
        _fixture = new ProcessWideApplicationTestFixture();
    }

    /// <summary>Disposes the process-wide fixture after all demonstration tests complete.</summary>
    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await _fixture.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Resets shared state between two process-wide scenarios.</summary>
    [TestMethod]
    public async Task SharedFixtureResetsStateBetweenScenarios()
    {
        var firstBookId = await _fixture.RunScenarioAsync(static async application =>
        {
            var book = await application.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
                new Book_CreateRequest.V1(new Book.V1.Create
                {
                    Title = "Process-wide fixture book",
                    Author = "Fixture",
                    Genre = Book.V1.Genre.Fiction,
                })).ConfigureAwait(false);
            return book.Id;
        }).ConfigureAwait(false);

        firstBookId.Should().NotBe(Guid.Empty);
        var secondScenarioBookCount = await _fixture.RunScenarioAsync(static async application =>
        {
            var page = await application.DispatchQueryAsync<Book_SearchQuery.V1, Book.V1.Page>(
                new Book_SearchQuery.V1()).ConfigureAwait(false);
            return page.Count;
        }).ConfigureAwait(false);

        secondScenarioBookCount.Should().Be(0);
        _fixture.Application.Network.Should().BeSameAs(_fixture.Network);
        _fixture.Processor.Should().NotBeNull();
    }

    /// <summary>Serializes concurrent scenario bodies through the process-wide fixture.</summary>
    [TestMethod]
    public async Task SharedFixtureSerializesConcurrentScenarios()
    {
        await Task.WhenAll(
            _fixture.RunScenarioAsync(static async _ =>
            {
                await Task.Delay(25).ConfigureAwait(false);
                return true;
            }),
            _fixture.RunScenarioAsync(static async _ =>
            {
                await Task.Delay(25).ConfigureAwait(false);
                return true;
            })).ConfigureAwait(false);

        _fixture.MaximumConcurrentScenarios.Should().Be(1);
    }

    /// <summary>Disposes all resources owned by a process-wide fixture.</summary>
    [TestMethod]
    public async Task ProcessWideFixtureDisposesResources()
    {
        var fixture = new ProcessWideApplicationTestFixture();
        await fixture.DisposeAsync().ConfigureAwait(false);

        fixture.IsDisposed.Should().BeTrue();
        var action = () => fixture.Application.DispatchRequestAsync<ScopeProbeRequest, Guid>(
            new ScopeProbeRequest());
        await action.Should().ThrowAsync<ObjectDisposedException>().ConfigureAwait(false);
    }
}
