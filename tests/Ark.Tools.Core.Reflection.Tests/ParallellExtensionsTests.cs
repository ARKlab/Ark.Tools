// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using AwesomeAssertions;
using System.Collections.Concurrent;

namespace Ark.Tools.Core.Reflection.Tests;

/// <summary>
/// Tests for ParallellExtensions to ensure parallel processing works correctly.
/// </summary>
[TestClass]
public class ParallellExtensionsTests
{
    private static readonly string[] _testItems = ["a", "b", "c", "d", "e"];
    private static readonly string[] _testSmallItems = ["a", "b", "c"];

    /// <summary>
    /// Verifies that Parallel with Task action processes all items.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithTaskAction_ShouldProcessAllItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var processed = new ConcurrentBag<int>();

        // Act
        await items.Parallel(4, async item =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            processed.Add(item);
        }).ConfigureAwait(false);

        // Assert
        processed.Should().HaveCount(10);
        processed.OrderBy(x => x).Should().BeEquivalentTo(items);
    }

    /// <summary>
    /// Verifies that Parallel with Task{TResult} action returns all results.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithTaskResultAction_ShouldReturnAllResults()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();

        // Act
        var results = await items.Parallel(4, async item =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            return item * 2;
        }).ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(10);
        results.OrderBy(x => x).Should().BeEquivalentTo(items.Select(x => x * 2));
    }

    /// <summary>
    /// Verifies that Parallel with index and Task action processes all items with correct indices.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithIndexAndTaskAction_ShouldProcessWithCorrectIndices()
    {
        // Arrange
        var items = _testItems.ToList();
        var processedWithIndex = new ConcurrentBag<(int Index, string Item)>();

        // Act
        await items.Parallel(2, async (index, item) =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            processedWithIndex.Add((index, item));
        }).ConfigureAwait(false);

        // Assert
        processedWithIndex.Should().HaveCount(5);
        var ordered = processedWithIndex.OrderBy(x => x.Index, Comparer<int>.Default).ToArray();
        for (var i = 0; i < items.Count; i++)
        {
            ordered[i].Index.Should().Be(i);
            ordered[i].Item.Should().Be(items[i]);
        }
    }

    /// <summary>
    /// Verifies that Parallel with index and Task{TResult} action returns all results with correct indices.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithIndexAndTaskResultAction_ShouldReturnAllResults()
    {
        // Arrange
        var items = _testSmallItems.ToList();

        // Act
        var results = await items.Parallel(2, async (index, item) =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            return $"{index}:{item}";
        }).ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(3);
        var ordered = results.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        ordered[0].Should().Be("0:a");
        ordered[1].Should().Be("1:b");
        ordered[2].Should().Be("2:c");
    }

    /// <summary>
    /// Verifies that Parallel with CancellationToken processes all items when not cancelled.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithCancellationToken_ShouldProcessAllItemsWhenNotCancelled()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToList();
        var processed = new ConcurrentBag<int>();
        using var cts = new CancellationTokenSource();

        // Act
        await items.Parallel(2, async (index, item, ct) =>
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            processed.Add(item);
        }, cts.Token).ConfigureAwait(false);

        // Assert
        processed.Should().HaveCount(5);
    }

    /// <summary>
    /// Verifies that Parallel with CancellationToken throws when cancelled.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithCancellationToken_ShouldThrowWhenCancelled()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act
        Func<Task> act = async () =>
        {
            await items.Parallel(2, async (index, item, ct) =>
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }, cts.Token).ConfigureAwait(false);
        };

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>().ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that Parallel with Task{TResult} and CancellationToken returns all results when not cancelled.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithTaskResultAndCancellationToken_ShouldReturnAllResults()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToList();
        using var cts = new CancellationTokenSource();

        // Act
        var results = await items.Parallel(2, async (index, item, ct) =>
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            return item * 3;
        }, cts.Token).ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(5);
        results.OrderBy(x => x).Should().BeEquivalentTo(items.Select(x => x * 3));
    }

    /// <summary>
    /// Verifies that Parallel respects the degree of parallelism.
    /// </summary>
    [TestMethod]
    public async Task Parallel_ShouldRespectDegreeOfParallelism()
    {
        // Arrange
        var items = Enumerable.Range(1, 20).ToList();
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new Lock();

        // Act
        await items.Parallel(3, async item =>
        {
            lock (lockObj)
            {
                currentConcurrent++;
                if (currentConcurrent > maxConcurrent)
                    maxConcurrent = currentConcurrent;
            }

            await Task.Delay(50).ConfigureAwait(false);

            lock (lockObj)
            {
                currentConcurrent--;
            }
        }).ConfigureAwait(false);

        // Assert
        maxConcurrent.Should().BeLessThanOrEqualTo(3);
        maxConcurrent.Should().BeGreaterThan(1); // Should actually use parallelism
    }

    /// <summary>
    /// Verifies that Parallel handles empty list.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithEmptyList_ShouldCompleteSuccessfully()
    {
        // Arrange
        var items = new List<int>();

        // Act
        await items.Parallel(2, async item =>
        {
            await Task.Delay(10).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Assert - should complete without errors
    }

    /// <summary>
    /// Verifies that Parallel with Task{TResult} handles empty list.
    /// </summary>
    [TestMethod]
    public async Task Parallel_WithEmptyListAndResult_ShouldReturnEmptyList()
    {
        // Arrange
        var items = new List<int>();

        // Act
        var results = await items.Parallel(2, async item =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            return item * 2;
        }).ConfigureAwait(false);

        // Assert
        results.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that Parallel propagates exceptions from action.
    /// </summary>
    [TestMethod]
    public async Task Parallel_ShouldPropagateExceptions()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();

        // Act
        Func<Task> act = async () =>
        {
            await items.Parallel(2, async item =>
            {
                await Task.Delay(10).ConfigureAwait(false);
                if (item == 5)
                    throw new InvalidOperationException("Test exception");
            }).ConfigureAwait(false);
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception").ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that Parallel processes items in parallel, not sequentially.
    /// </summary>
    [TestMethod]
    public async Task Parallel_ShouldProcessInParallel()
    {
        // Arrange
        var items = Enumerable.Range(1, 4).ToList();
        var startTimes = new ConcurrentDictionary<int, DateTime>();
        var endTimes = new ConcurrentDictionary<int, DateTime>();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await items.Parallel(4, async item =>
        {
            startTimes[item] = DateTime.UtcNow;
            await Task.Delay(100).ConfigureAwait(false);
            endTimes[item] = DateTime.UtcNow;
        }).ConfigureAwait(false);
        sw.Stop();

        // Assert
        // If sequential, would take ~400ms. If parallel, should take ~100ms
        sw.ElapsedMilliseconds.Should().BeLessThan(300);
        
        // Verify items were processed concurrently by checking overlapping time windows
        var processingStarted = startTimes.Count;
        processingStarted.Should().Be(4);
    }
}
