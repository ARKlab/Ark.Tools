// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class SseTests
{
    public sealed record Probe : IQuery<Probe, ProbeResult>;

    public sealed record ProbeResult(string ETag, int Value);

    [TestMethod]
    public async Task PollEmitsOneFrameForEachChangeAndHeartbeatsWhenIdle()
    {
        var processor = new StubQueryProcessor([new("a", 1), new("a", 1), new("b", 2)]);
        var body = await _runPollAsync(processor, static result => result.ETag, settings: _settings());

        var events = _events(body);
        events.Where(static x => x.Event == "probe").Should().HaveCount(2);
        events.Should().Contain(static x => x.Event == ArkSse.HeartbeatEventName);
        events.Where(static x => x.Event == "probe").Select(static x => x.Id).Should().Equal("a", "b");
    }

    [TestMethod]
    public async Task PollWithoutETagComparesSerializedPayloads()
    {
        var processor = new StubQueryProcessor([new("x", 1), new("x", 1), new("x", 3)]);
        var body = await _runPollAsync(processor, changeToken: null, settings: _settings());

        _events(body).Where(static x => x.Event == "probe").Should().HaveCount(2);
    }

    [TestMethod]
    public async Task PollEmitsEveryTickWhenChangeDetectionIsDisabled()
    {
        var processor = new StubQueryProcessor([new("a", 1), new("a", 1), new("a", 1)]);
        var body = await _runPollAsync(
            processor,
            static result => result.ETag,
            _settings(emitEveryTick: true));

        _events(body).Where(static x => x.Event == "probe").Should().HaveCount(3);
    }

    [TestMethod]
    public async Task PollSkipsTheFirstFrameWhenLastEventIdMatches()
    {
        var processor = new StubQueryProcessor([new("a", 1), new("b", 2)]);
        var body = await _runPollAsync(
            processor,
            static result => result.ETag,
            _settings(),
            static context => context.Request.Headers["Last-Event-ID"] = "a");

        _events(body).Where(static x => x.Event == "probe").Select(static x => x.Id).Should().Equal("b");
    }

    [TestMethod]
    public async Task PollStopsExecutingWhenTheClientDisconnects()
    {
        var processor = new StubQueryProcessor([new("a", 1), new("b", 2)]);
        await _runPollAsync(processor, static result => result.ETag, _settings(), disconnect: true);

        processor.Executions.Should().Be(2);
    }

    [TestMethod]
    public void ResolveIntervalClampsTheClientRequestToTheDeclaredBounds()
    {
        var settings = new ArkSseSettings(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(30),
            allowClientInterval: true,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(60),
            emitEveryTick: false,
            "probe");

        _resolve(settings, "1").Should().Be(TimeSpan.FromSeconds(2));
        _resolve(settings, "600").Should().Be(TimeSpan.FromSeconds(30));
        _resolve(settings, "7").Should().Be(TimeSpan.FromSeconds(7));
        _resolve(settings, "not-a-number").Should().Be(TimeSpan.FromSeconds(5));
        _resolve(settings, null).Should().Be(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void ResolveIntervalIgnoresTheClientWhenTheContractDoesNotAllowIt()
    {
        var settings = new ArkSseSettings(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(30),
            allowClientInterval: false,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(60),
            emitEveryTick: false,
            "probe");

        _resolve(settings, "29").Should().Be(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void ResolveIntervalRaisesAZeroDeclaredIntervalToTheFloor()
    {
        var settings = new ArkSseSettings(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(30),
            allowClientInterval: false,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(60),
            emitEveryTick: false,
            "probe");

        _resolve(settings, null).Should().Be(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task PollReturnsServiceUnavailableWhenTheConnectionCapIsReached()
    {
        var tracker = new ArkSseConnectionTracker(maxConcurrentConnections: 1);
        using var held = tracker.TryAcquire("someone-else");
        held.Should().NotBeNull();

        var context = _context(new StubQueryProcessor([]), tracker);
        var result = ArkSse.Poll<Probe, ProbeResult>(
            context,
            new Probe(),
            _settings(),
            static x => x.ETag,
            CancellationToken.None);

        await result.ExecuteAsync(context).ConfigureAwait(false);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
        tracker.Count.Should().Be(1);
    }

    [TestMethod]
    public void ConnectionTrackerCapsConnectionsPerPrincipal()
    {
        var tracker = new ArkSseConnectionTracker(maxConcurrentConnections: 10, maxConcurrentConnectionsPerPrincipal: 1);

        var first = tracker.TryAcquire("alice");
        first.Should().NotBeNull();
        tracker.TryAcquire("alice").Should().BeNull();
        tracker.TryAcquire("bob").Should().NotBeNull();

        first!.Dispose();
        first.Dispose();
        tracker.TryAcquire("alice").Should().NotBeNull();
    }

    [TestMethod]
    public async Task StreamFramesEveryItemOfAStreamingQuery()
    {
        var context = _context(new StubQueryProcessor([]));
        var result = ArkSse.Stream(context, _settings(), _items(), CancellationToken.None);

        var body = await _executeAsync(context, result).ConfigureAwait(false);

        _events(body).Select(static x => x.Event).Should().Equal("probe", "probe", "probe");
    }

    private static async IAsyncEnumerable<int> _items()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    private static ArkSseSettings _settings(bool emitEveryTick = false) => new(
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromSeconds(1),
        allowClientInterval: false,
        TimeSpan.FromMilliseconds(1),
        TimeSpan.Zero,
        emitEveryTick,
        "probe");

    private static TimeSpan _resolve(ArkSseSettings settings, string? requested)
    {
        var context = new DefaultHttpContext();
        if (requested is not null)
            context.Request.QueryString = new QueryString("?" + ArkSse.IntervalParameterName + "=" + requested);
        return ArkSse.ResolveInterval(context, settings);
    }

    private static async Task<string> _runPollAsync(
        StubQueryProcessor processor,
        Func<ProbeResult, string?>? changeToken,
        ArkSseSettings settings,
        Action<HttpContext>? configure = null,
        bool disconnect = false)
    {
        var context = _context(processor);
        configure?.Invoke(context);
        using var abort = new CancellationTokenSource();
        if (disconnect)
            processor.Exhausted = abort;
        context.RequestAborted = abort.Token;

        var result = ArkSse.Poll(context, new Probe(), settings, changeToken, abort.Token);
        try
        {
            return await _executeAsync(context, result).ConfigureAwait(false);
        }
        finally
        {
            if (context.RequestServices is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (context.RequestServices is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static async Task<string> _executeAsync(HttpContext context, IResult result)
    {
        using var body = new MemoryStream();
        context.Response.Body = body;
        try
        {
            await result.ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A closed SSE connection surfaces as a cancelled write, exactly as it does on a real host.
        }

        return Encoding.UTF8.GetString(body.ToArray());
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The container lives for the duration of the test process.")]
    private static DefaultHttpContext _context(StubQueryProcessor processor, ArkSseConnectionTracker? tracker = null)
    {
        var container = new Container();
        container.RegisterInstance<IQueryProcessor>(processor);
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .AddSingleton(container);
        if (tracker is not null)
            services.AddSingleton(tracker);

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static List<(string? Event, string? Id)> _events(string body)
    {
        var events = new List<(string? Event, string? Id)>();
        string? name = null;
        string? id = null;
        foreach (var text in body.Split('\n').Select(static line => line.TrimEnd('\r')))
        {
            if (text.Length == 0)
            {
                if (name is not null)
                    events.Add((name, id));
                name = null;
                id = null;
                continue;
            }

            if (text.StartsWith("event:", StringComparison.Ordinal))
                name = text["event:".Length..].Trim();
            else if (text.StartsWith("id:", StringComparison.Ordinal))
                id = text["id:".Length..].Trim();
        }

        return events;
    }

    private sealed class StubQueryProcessor : IQueryProcessor
    {
        private readonly Queue<ProbeResult> _results;

        public StubQueryProcessor(IEnumerable<ProbeResult> results)
        {
            _results = new Queue<ProbeResult>(results);
        }

        public CancellationTokenSource? Exhausted { get; set; }

        public int Executions { get; private set; }

        [Obsolete("Synchronous execution is not supported.", error: true)]
        public TResult Execute<TResult>(IQuery<TResult> query) => throw new NotSupportedException();

        public Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default)
            => throw new NotSupportedException();

        public async Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
            where TQuery : class, IQuery<TQuery, TResult>
        {
            await Task.Yield();
            ctk.ThrowIfCancellationRequested();
            Executions++;
            // An exhausted stub ends the connection the way a real one does: either the client goes
            // away (cancellation token) or the poll is cancelled while awaiting the next result.
            if (_results.Count == 0)
                throw new OperationCanceledException();

            var result = (TResult)(object)_results.Dequeue();
            if (_results.Count == 0 && Exhausted is not null)
                await Exhausted.CancelAsync().ConfigureAwait(false);
            return result;
        }
    }
}
