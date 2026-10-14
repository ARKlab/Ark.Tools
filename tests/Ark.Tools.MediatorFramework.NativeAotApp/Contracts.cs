// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.MediatorFramework.Mcp;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

using System.Text.Json.Serialization;

using Vogen;

namespace Ark.Tools.MediatorFramework.NativeAotApp;

/// <summary>A Vogen value object over a <see cref="Guid"/>.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct BookId;

/// <summary>A Vogen value object over a <see cref="string"/>.</summary>
/// <remarks>
/// Not a number: Vogen's converter for a numeric primitive calls the reflection-based <c>JsonSerializer</c>, which
/// Native AOT reports, unless it opts into <c>Customizations.TreatNumberAsStringInSystemTextJson</c>.
/// </remarks>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct BookCode;

/// <summary>A book.</summary>
public sealed record Book
{
    /// <summary>Gets the identifier.</summary>
    public BookId Id { get; init; }

    /// <summary>Gets the optional parent identifier.</summary>
    public BookId? ParentId { get; init; }

    /// <summary>Gets the code.</summary>
    public BookCode Code { get; init; }

    /// <summary>Gets the related identifiers.</summary>
    public IReadOnlyList<BookId> Related { get; init; } = [];
}

/// <summary>A row shredded by <c>ToDataTableArk()</c>.</summary>
public sealed class BookRow
{
    /// <summary>Gets or sets the identifier.</summary>
    public BookId Id { get; set; }

    /// <summary>Gets or sets the optional parent identifier.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the code.</summary>
    public BookCode Code { get; set; }
}

/// <summary>A row without value objects, shredded by the reflection fallback.</summary>
public sealed class PlainRow
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optional parent identifier.</summary>
    public Guid? ParentId { get; set; }
}

/// <summary>Reads a book through a generated Minimal API endpoint and a generated MCP tool.</summary>
[HttpEndpoint("GET", "/books/{id}")]
[McpTool(Name = "books.get")]
public sealed record GetBookQuery : IQuery<GetBookQuery, Book>
{
    /// <summary>Gets the book identifier.</summary>
    public BookId Id { get; init; }
}

/// <summary>Handles <see cref="GetBookQuery"/>.</summary>
public sealed class GetBookQueryHandler : IQueryHandler<GetBookQuery, Book>
{
    /// <inheritdoc />
    public async Task<Book> ExecuteAsync(GetBookQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult(new Book { Id = query.Id, Code = BookCode.From("B-1"), Related = [query.Id] }).ConfigureAwait(false);
    }
}

/// <summary>Uploads a book file through a generated MCP tool.</summary>
[McpTool(Name = "books.upload")]
public sealed record UploadBookRequest : IRequest<UploadBookRequest, Book>
{
    /// <summary>Gets or sets the uploaded file.</summary>
    public IArkAttachment? Attachment { get; set; }
}

/// <summary>Handles <see cref="UploadBookRequest"/>.</summary>
public sealed class UploadBookRequestHandler : IRequestHandler<UploadBookRequest, Book>
{
    /// <inheritdoc />
    public async Task<Book> ExecuteAsync(UploadBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var attachment = request.Attachment ?? throw new InvalidOperationException("The attachment is missing.");
        var stream = attachment.OpenRead();
        await using var _ = stream.ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(ctk).ConfigureAwait(false);
        return new Book { Id = BookId.From(Guid.Empty), Code = BookCode.From(attachment.Name + ":" + text) };
    }
}

/// <summary>Downloads a book file through a generated MCP tool.</summary>
[McpTool(Name = "books.download")]
public sealed record DownloadBookQuery : IQuery<DownloadBookQuery, IArkAttachment>
{
    /// <summary>Gets or sets the file name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Handles <see cref="DownloadBookQuery"/>.</summary>
public sealed class DownloadBookQueryHandler : IQueryHandler<DownloadBookQuery, IArkAttachment>
{
    /// <inheritdoc />
    public async Task<IArkAttachment> ExecuteAsync(DownloadBookQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult<IArkAttachment>(new ArkAttachment(query.Name, "text/plain", static () => new MemoryStream("payload"u8.ToArray()))).ConfigureAwait(false);
    }
}

/// <summary>Selects this assembly for the generated MCP tools.</summary>
[ArkGenerateMcpToolsForAssembly(typeof(GetBookQuery))]
public partial class AppMcpContext;

/// <summary>Selects this assembly for the generated Minimal API endpoints.</summary>
[ArkGenerateMinimalApiForAssembly(typeof(GetBookQuery))]
public partial class AppEndpointContext;

/// <summary>Source-generated JSON metadata: it cannot see the converters Vogen generates.</summary>
[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Book))]
[JsonSerializable(typeof(GetBookQuery))]
[JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
[JsonSerializable(typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails))]
public sealed partial class AppJsonContext : JsonSerializerContext;
