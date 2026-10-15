// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;

using AwesomeAssertions;

using OpenTelemetry;
using OpenTelemetry.Trace;

using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that the parent read from an incoming Rebus message is remote, so
/// <see cref="ArkAdaptiveSamplerOptions.RespectRemoteParentSampling"/> decides whether the sender's flag is followed.
/// </summary>
[TestClass]
// Every tracer provider listening to the Rebus source votes on its spans: keep the others away.
[DoNotParallelize]
public sealed class RebusRemoteParentSamplingTests
{
    private const string _sampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
    private const string _unsampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";

    [TestMethod]
    [DataRow(_sampledParent)]
    [DataRow(_unsampledParent)]
    public async Task IncomingMessage_HasRemoteParent(string diagnosticId)
    {
        var activity = await _processAsync(diagnosticId, tracesPerSecond: 10_000, respectRemoteParentSampling: false).ConfigureAwait(false);

        activity.HasRemoteParent.Should().BeTrue();
        activity.TraceId.ToHexString().Should().Be("0af7651916cd43dd8448eb211c80319c");
        activity.ParentSpanId.ToHexString().Should().Be("b7ad6b7169203331");
    }

    [TestMethod]
    public async Task IncomingMessage_UnsampledParent_IsSampledByTheBudget()
    {
        var activity = await _processAsync(_unsampledParent, tracesPerSecond: 10_000, respectRemoteParentSampling: false).ConfigureAwait(false);

        activity.Recorded.Should().BeTrue("the worker's own budget decides, not the sender's flag");
        activity.GetTagItem("microsoft.sample_rate").Should().Be(100.0);
    }

    [TestMethod]
    public async Task IncomingMessage_SampledParent_IsDroppedByAnExhaustedBudget()
    {
        var activity = await _processAsync(_sampledParent, tracesPerSecond: 0.0001, respectRemoteParentSampling: false).ConfigureAwait(false);

        activity.Recorded.Should().BeFalse("the worker's own budget decides, not the sender's flag");
    }

    [TestMethod]
    [DataRow(_sampledParent, true)]
    [DataRow(_unsampledParent, false)]
    public async Task IncomingMessage_RespectRemoteParentSampling_FollowsTheFlag(string diagnosticId, bool expectedRecorded)
    {
        // The budget would decide the other way: an exhausted one for the sampled parent, an ample one for the unsampled.
        var activity = await _processAsync(
            diagnosticId,
            tracesPerSecond: expectedRecorded ? 0.0001 : 10_000,
            respectRemoteParentSampling: true).ConfigureAwait(false);

        activity.HasRemoteParent.Should().BeTrue();
        activity.Recorded.Should().Be(expectedRecorded);
        activity.GetTagItem("microsoft.sample_rate").Should().BeNull("the upstream rate is not propagated");
    }

    private static async Task<Activity> _processAsync(string diagnosticId, double tracesPerSecond, bool respectRemoteParentSampling)
    {
        var registry = new FailedTraceRegistry();
        var sampler = new ArkAdaptiveSampler(
            new ArkAdaptiveSamplerOptions
            {
                TracesPerSecond = tracesPerSecond,
                EnablePerOperationBucketing = false,
                SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(10),
                RespectRemoteParentSampling = respectRemoteParentSampling,
            },
            registry);
        using var promotion = new ArkFailurePromotionProcessor(registry);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(OpenTelemetryStep.ActivitySourceName)
            .SetSampler(sampler)
            .AddProcessor(promotion)
            .Build();
        using var scope = new RebusTransactionScope();
        var container = new Container();
        await using var containerScope = container.ConfigureAwait(false);
        var message = new TransportMessage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Headers.Type] = "tests.Message",
                ["Diagnostic-Id"] = diagnosticId,
            },
            []);
        var step = new OpenTelemetryStep(container);
        Activity? activity = null;

        await step.Process(new IncomingStepContext(message, scope.TransactionContext), () =>
        {
            activity = Activity.Current;
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        activity.Should().NotBeNull("the Ark sampler records spans it does not sample");
        return activity!;
    }
}
