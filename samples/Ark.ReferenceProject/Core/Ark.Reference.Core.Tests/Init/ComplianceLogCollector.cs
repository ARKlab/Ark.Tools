// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.Compliance.NLog;

using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

using System.Collections.Concurrent;

namespace Ark.Reference.Core.Tests.Init;

internal sealed class ComplianceLogCollector : IDisposable
{
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly LoggingConfiguration _configuration;
    private readonly CollectorTarget _target;
    private readonly LoggingRule _rule;

    internal ComplianceLogCollector()
    {
        _configuration = LogManager.Configuration ?? new LoggingConfiguration();
        _target = new CollectorTarget(_messages);
        _configuration.AddTarget("ComplianceTest", _target);
        _rule = new LoggingRule("Ark.Reference.Core.Application.*", LogLevel.Trace, _target);
        _configuration.AddRule(_rule);
        LogManager.Configuration = _configuration;
    }

    internal IReadOnlyCollection<string> _getMessages()
    {
        return _messages.ToArray();
    }

    internal void _reset()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
        LogManager.Flush(TimeSpan.FromSeconds(2));
        _configuration.RemoveRule(_rule);
        _configuration.RemoveTarget("ComplianceTest");
        LogManager.Configuration = _configuration;
    }

    private sealed class CollectorTarget : TargetWithLayout
    {
        private readonly ConcurrentQueue<string> _messages;

        internal CollectorTarget(ConcurrentQueue<string> messages)
        {
            _messages = messages;
            Layout = new ComplianceLayout(
                NLog.Layouts.Layout.FromString("${message}"),
                static () => new PiiScanner(PiiScanMode.MessageAndProperties));
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            try
            {
                _messages.Enqueue(RenderLogEvent(Layout, logEvent.LogEvent));
                logEvent.Continuation(null);
            }
            catch (Exception exception)
            {
                logEvent.Continuation(exception);
            }
        }
    }
}
