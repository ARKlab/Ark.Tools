// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Named composition-time failures for the messaging processing side.</summary>
public enum MessagingCompositionDiagnostic
{
    /// <summary>The transport does not implement <see cref="IMessagingMessageSource"/>.</summary>
    TransportIsNotAMessageSource,

    /// <summary>A processor host was composed in a host that owns triggering itself.</summary>
    ProcessorHostInTriggeredHost,

    /// <summary>The supplied <see cref="MessagingProcessingOptions"/> cannot be satisfied.</summary>
    ProcessingOptionsInvalid
}

/// <summary>Thrown when messaging composition fails with a named diagnostic.</summary>
public sealed class MessagingCompositionException : InvalidOperationException
{
    /// <summary>Creates a composition exception with a named diagnostic.</summary>
    /// <param name="diagnostic">The named diagnostic.</param>
    /// <param name="message">The human-readable explanation.</param>
    public MessagingCompositionException(MessagingCompositionDiagnostic diagnostic, string message)
        : base(FormattableString.Invariant($"[{diagnostic}] {message}"))
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Creates a composition exception without a diagnostic.</summary>
    public MessagingCompositionException()
    {
    }

    /// <summary>Creates a composition exception with a message.</summary>
    /// <param name="message">The human-readable explanation.</param>
    public MessagingCompositionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a composition exception with a message and an inner exception.</summary>
    /// <param name="message">The human-readable explanation.</param>
    /// <param name="innerException">The cause.</param>
    public MessagingCompositionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the named diagnostic.</summary>
    public MessagingCompositionDiagnostic Diagnostic { get; }
}
