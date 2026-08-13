// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Application.Handlers.Book;

/// <summary>Describes a polymorphic Book edition without transport dependencies.</summary>
public sealed class DescribeBookEditionHandler : IRequestHandler<DescribeBookEditionRequest, BookEditionDescription>
{
    /// <inheritdoc />
    public async Task<BookEditionDescription> ExecuteAsync(
        DescribeBookEditionRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var description = request.Edition switch
        {
            PrintBookEdition print => $"{print.Format} print edition with {print.PageCount} pages",
            DigitalBookEdition digital => $"{digital.Format} digital edition with {digital.SizeBytes} bytes",
            _ => throw new NotSupportedException($"Unknown Book edition '{request.Edition.GetType().Name}'."),
        };

        await Task.CompletedTask.ConfigureAwait(false);
        return new BookEditionDescription
        {
            Edition = request.Edition,
            Description = description,
        };
    }
}
