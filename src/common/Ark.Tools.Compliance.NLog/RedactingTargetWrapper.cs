// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Common;
using global::NLog.Targets;
using global::NLog.Targets.Wrappers;

using System.Runtime.CompilerServices;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Sanitizes events before downstream targets, including asynchronous queues.</summary>
[Target("ArkComplianceRedaction")]
public sealed class RedactingTargetWrapper : WrapperTargetBase
{
    private RedactedEventCache _cache;

    /// <summary>Initializes a wrapper with a fail-closed policy.</summary>
    /// <param name="target">The downstream target.</param>
    /// <param name="options">Optional policy overrides.</param>
    public RedactingTargetWrapper(Target target, ComplianceRedactionOptions? options = null)
        : this(target, new RedactedEventCache(new ComplianceRedactor(options)))
    {
    }

    internal RedactingTargetWrapper(Target target, RedactedEventCache cache)
    {
        ArgumentNullException.ThrowIfNull(target);
        WrappedTarget = target;
        _cache = cache;
    }

    internal void _setCache(RedactedEventCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    protected override void Write(AsyncLogEventInfo logEvent)
    {
        WrappedTarget!.WriteAsyncLogEvent(new AsyncLogEventInfo(_cache._get(logEvent.LogEvent), logEvent.Continuation));
    }
}

internal sealed class RedactedEventCache(ComplianceRedactor? redactor)
{
    private readonly ConditionalWeakTable<LogEventInfo, LogEventInfo> _events = new();

    internal LogEventInfo _get(LogEventInfo source)
    {
        return _events.GetValue(source, _redact);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "An unsafe event must never be forwarded on a redaction failure.")]
    [SuppressMessage("Correctness", "ERP022", Justification = "Discard confidential exception details at this fail-closed boundary.")]
    private LogEventInfo _redact(LogEventInfo source)
    {
        if (redactor is null)
            return source;
        try
        {
            var result = new LogEventInfo(source.Level, source.LoggerName, CultureInfo.InvariantCulture,
                source.Message, source.Parameters?.Select(redactor.Redact).ToArray())
            {
                TimeStamp = source.TimeStamp,
                Exception = source.Exception is null ? null : new InvalidOperationException(ComplianceRedactor.Marker),
            };
            foreach (var property in source.Properties)
            {
                var key = property.Key is string name ? redactor.Scan(name) : ComplianceRedactor.Marker;
                result.Properties[key] = redactor.Redact(property.Value);
            }
            if (redactor.PatternScan != PatternScanMode.Off)
            {
                var message = redactor.Scan(result.FormattedMessage);
                result.Message = message;
                result.Parameters = null;
            }
            return _copyCallSite(source, result);
        }
        catch (Exception ex) when (!_isCriticalException(ex))
        {
            return _copyCallSite(source, new(source.Level, source.LoggerName, ComplianceRedactor.Marker));
        }
    }

    private static LogEventInfo _copyCallSite(LogEventInfo source, LogEventInfo target)
    {
        var callerClassName = source.CallerClassName;
        var callerMemberName = source.CallerMemberName;
        var callerFilePath = source.CallerFilePath;
        var callerLineNumber = source.CallerLineNumber;
        if (callerClassName is not null || callerMemberName is not null || callerFilePath is not null || callerLineNumber != 0)
            target.SetCallerInfo(callerClassName, callerMemberName, callerFilePath, callerLineNumber);
        if (source.StackTrace is { } stackTrace)
            target.SetStackTrace(stackTrace);
        return target;
    }

    private static bool _isCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or ThreadAbortException;
    }
}
