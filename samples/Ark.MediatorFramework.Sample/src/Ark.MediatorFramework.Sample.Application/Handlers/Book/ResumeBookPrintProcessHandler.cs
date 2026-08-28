// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Resumes a queued book print process through the request pipeline.</summary>
public sealed class ResumeBookPrintProcessHandler :
    IRequestHandler<ResumeBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly ProcessBookPrintProcessHandler _processHandler;

    /// <summary>Initializes a new instance of the <see cref="ResumeBookPrintProcessHandler"/> class.</summary>
    /// <param name="processHandler">The command handler that executes the process.</param>
    public ResumeBookPrintProcessHandler(ProcessBookPrintProcessHandler processHandler)
    {
        _processHandler = processHandler;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        ResumeBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _processHandler.ExecuteAsync(
            new ProcessBookPrintProcessRequest { Id = request.Id },
            ctk).ConfigureAwait(false);
    }
}
