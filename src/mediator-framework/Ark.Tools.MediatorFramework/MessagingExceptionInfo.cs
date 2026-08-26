// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Serializable, bounded information about a messaging exception.</summary>
public sealed record MessagingExceptionInfo(
    string ExceptionType,
    string Message,
    string? StackTrace,
    MessagingExceptionInfo? Inner)
{
    private const int _maximumMessageLength = 256;
    private const int _maximumStackTraceLength = 4_096;
    private const int _maximumInnerDepth = 8;

    /// <summary>Creates a bounded exception snapshot.</summary>
    /// <param name="exception">The exception to snapshot.</param>
    /// <returns>The serializable snapshot.</returns>
    public static MessagingExceptionInfo From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return _from(exception, 0);
    }

    private static MessagingExceptionInfo _from(Exception exception, int depth)
    {
        return new MessagingExceptionInfo(
            exception.GetType().FullName ?? exception.GetType().Name,
            _truncate(exception.Message, _maximumMessageLength),
            _truncate(exception.StackTrace, _maximumStackTraceLength),
            depth < _maximumInnerDepth && exception.InnerException is not null
                ? _from(exception.InnerException, depth + 1)
                : null);
    }

    private static string _truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
