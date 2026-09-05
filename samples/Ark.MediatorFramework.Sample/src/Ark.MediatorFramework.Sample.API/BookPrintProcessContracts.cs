// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Defines the lifecycle states of a book print process.</summary>
public enum BookPrintProcessStatus
{
    /// <summary>The process has not been initialized.</summary>
    NOT_SET = 0,

    /// <summary>The process is waiting for background execution.</summary>
    Pending,

    /// <summary>The process is being executed.</summary>
    Running,

    /// <summary>The process completed successfully.</summary>
    Completed,

    /// <summary>The process completed with an error.</summary>
    Error,

    /// <summary>The process was cancelled before completion.</summary>
    Cancelled,
}

/// <summary>Represents a background book print process.</summary>
public sealed record BookPrintProcessResponse
{
    /// <summary>Gets the print-process identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the book being printed.</summary>
    public Guid BookId { get; init; }

    /// <summary>Gets the progress fraction.</summary>
    public double Progress { get; init; }

    /// <summary>Gets the current process status.</summary>
    public EvolvableEnum<BookPrintProcessStatus> Status { get; init; }

    /// <summary>Gets the error details when the process fails.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets whether the test process should report an error.</summary>
    public bool ShouldFail { get; init; }
}

/// <summary>Starts a background print process for a book.</summary>
public static class CreateBookPrintProcessRequest
{
    /// <summary>Version one of the book print-process creation request.</summary>
    public sealed record V1 : IRequest<V1, BookPrintProcessResponse>
    {
        /// <summary>Gets the identifier of the book to print.</summary>
        public Guid BookId { get; init; }

        /// <summary>Gets whether the process should report an error.</summary>
        public bool ShouldFail { get; init; }
    }
}

/// <summary>Reads a book print process by identifier.</summary>
public static class GetBookPrintProcessQuery
{
    /// <summary>Version one of the book print-process query.</summary>
    public sealed record V1 : IQuery<V1, BookPrintProcessResponse>
    {
        /// <summary>Gets the print-process identifier.</summary>
        public Guid Id { get; init; }
    }
}

/// <summary>Cancels a pending or running book print process.</summary>
public static class CancelBookPrintProcessRequest
{
    /// <summary>Version one of the book print-process cancellation request.</summary>
    public sealed record V1 : IRequest<V1, BookPrintProcessResponse>
    {
        /// <summary>Gets the print-process identifier.</summary>
        public Guid Id { get; init; }
    }
}
