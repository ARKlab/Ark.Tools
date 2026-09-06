// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.Messaging;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Ark.Tools.Benchmarks;

/// <summary>
/// Measures how much throughput the batching, prefetching and adaptive-concurrency runtime buys over
/// the single-message receive-handle-settle loop it replaced.
/// </summary>
/// <remarks>
/// The workload is I/O-bound by construction (a short asynchronous wait per delivery), because that is
/// the shape the runtime targets: a compute-bound handler cannot go faster than the cores it has. The
/// in-memory transport is used on purpose, so the number measures the host and not a broker.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class MessagingThroughputBenchmarks
{
    /// <summary>Runs in process: every invocation drains a whole backlog, so process isolation buys nothing.</summary>
    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithStrategy(RunStrategy.Monitoring)
                .WithWarmupCount(1)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
        }
    }

    private const string _queue = "benchmark-queue";
    private static readonly TimeSpan _handlerWork = TimeSpan.FromMilliseconds(2);
    private static readonly ReadOnlyMemory<byte> _payload = new byte[] { 1, 2, 3, 4 };

    private InMemoryMessagingTransport _transport = null!;

    /// <summary>Gets or sets the number of messages drained per invocation.</summary>
    [Params(2000)]
    public int MessageCount { get; set; }

    /// <summary>Fills a fresh queue before every invocation so the two loops drain the same backlog.</summary>
    [IterationSetup]
    public void FillQueue()
    {
        _transport = new InMemoryMessagingTransport();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < MessageCount; index++)
        {
            _transport
                .SendAsync(_queue, headers, new ReadOnlySequence<byte>(_payload), null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
    }

    /// <summary>Drains the backlog one message at a time, the pre-AMF behaviour.</summary>
    /// <returns>A task that completes when the backlog is drained.</returns>
    [Benchmark(Baseline = true, Description = "Sequential receive-handle-settle")]
    public async Task Sequential()
    {
        var source = (IMessagingMessageSource)_transport;
        var drained = 0;
        while (drained < MessageCount)
        {
            var batch = await source
                .ReceiveBatchAsync(_queue, 1, TimeSpan.FromSeconds(1), CancellationToken.None)
                .ConfigureAwait(false);
            foreach (var delivery in batch)
            {
                await _handleAsync(delivery, CancellationToken.None).ConfigureAwait(false);
                drained++;
            }
        }
    }

    /// <summary>Drains the same backlog through the host with a fixed concurrency.</summary>
    /// <returns>A task that completes when the backlog is drained.</returns>
    [Benchmark(Description = "MessagingProcessorHost, fixed concurrency")]
    public async Task ProcessorHostFixedConcurrency()
    {
        await _drainAsync(new MessagingProcessingOptions
        {
            AdaptiveConcurrency = false,
            InitialConcurrency = 32,
            MinimumConcurrency = 1,
            MaximumConcurrency = 32,
            ExpectedHandlerDuration = _handlerWork,
        }).ConfigureAwait(false);
    }

    /// <summary>Drains the same backlog while the controller searches for the limit.</summary>
    /// <returns>A task that completes when the backlog is drained.</returns>
    /// <remarks>
    /// The evaluation interval is shortened because a benchmark drain lasts about a second: with the
    /// five-second default the controller would never take a second reading, and the number would say
    /// nothing about adaptation.
    /// </remarks>
    [Benchmark(Description = "MessagingProcessorHost, adaptive concurrency")]
    public async Task ProcessorHostAdaptiveConcurrency()
    {
        await _drainAsync(new MessagingProcessingOptions
        {
            AdaptiveConcurrency = true,
            ConcurrencyEvaluationInterval = TimeSpan.FromMilliseconds(50),
            InitialConcurrency = 4,
            MinimumConcurrency = 1,
            MaximumConcurrency = 32,
            ExpectedHandlerDuration = _handlerWork,
        }).ConfigureAwait(false);
    }

    private async Task _drainAsync(MessagingProcessingOptions options)
    {
        var processed = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MessagingProcessorHost(
            _transport,
            _queue,
            async (delivery, ctk) =>
            {
                await _handleAsync(delivery, ctk).ConfigureAwait(false);
                if (Interlocked.Increment(ref processed) == MessageCount)
                    completed.TrySetResult();
            },
            options);

        await using (host.ConfigureAwait(false))
        {
            await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
            await completed.Task.WaitAsync(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task _handleAsync(IMessagingLockedDelivery delivery, CancellationToken ctk)
    {
        // A short asynchronous wait stands in for the network call a real handler makes: it is what
        // makes concurrency worth anything at all.
        await Task.Delay(_handlerWork, ctk).ConfigureAwait(false);
        await delivery.CompleteAsync(ctk).ConfigureAwait(false);
    }
}
