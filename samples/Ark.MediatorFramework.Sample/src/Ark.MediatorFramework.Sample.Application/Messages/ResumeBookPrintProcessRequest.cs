// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Resumes a book print process through the application request pipeline.</summary>
public sealed record ResumeBookPrintProcessRequest :
    IRequest<ResumeBookPrintProcessRequest, BookPrintProcessResponse>
{
    /// <summary>Gets the print-process identifier.</summary>
    public Guid Id { get; init; }
}
