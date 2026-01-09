// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;


namespace Ark.ResourceWatcher.Sample.Tests.Mocks;

/// <summary>
/// Mock sink API for testing.
/// </summary>
public sealed class MockSinkApi
{
    private readonly List<SinkDto> _receivedPayloads = [];
    private readonly List<string> _callHistory = [];
    private int _failCount;
    private bool _alwaysFail;

    /// <summary>
    /// Gets all payloads received by the sink.
    /// </summary>
    public IReadOnlyList<SinkDto> ReceivedPayloads => _receivedPayloads;

    /// <summary>
    /// Gets the call history.
    /// </summary>
    public IReadOnlyList<string> CallHistory => _callHistory;

    /// <summary>
    /// Gets the total number of records received.
    /// </summary>
    public int TotalRecordsReceived => _receivedPayloads.Sum(p => p.Records?.Count ?? 0);

    /// <summary>
    /// Configures the mock to fail on the next N calls.
    /// </summary>
    /// <param name="count">Number of calls that should fail.</param>
    public void FailNextCalls(int count)
    {
        _failCount = count;
    }

    /// <summary>
    /// Configures the mock to always fail.
    /// </summary>
    public void AlwaysFail()
    {
        _alwaysFail = true;
    }

    /// <summary>
    /// Receives a payload from the worker.
    /// </summary>
    /// <param name="payload">The sink payload.</param>
    /// <returns>True if accepted, false if rejected.</returns>
    public bool Receive(SinkDto payload)
    {
        _callHistory.Add(payload.SourceId ?? "unknown");

        if (_alwaysFail)
        {
            return false;
        }

        if (_failCount > 0)
        {
            _failCount--;
            return false;
        }

        _receivedPayloads.Add(payload);
        return true;
    }

    /// <summary>
    /// Clears all received payloads and resets failure settings.
    /// </summary>
    public void Reset()
    {
        _receivedPayloads.Clear();
        _callHistory.Clear();
        _failCount = 0;
        _alwaysFail = false;
    }
}