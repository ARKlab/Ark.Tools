using Ark.Tools.ResourceWatcher;

using NodaTime;

using System;
using System.Collections.Generic;

namespace TestWorker.Dto;

public class Test_FileMetadataDto : IResourceMetadata
{
    public string? FileName { get; set; }
    public LocalDate Date { get; set; }

    string IResourceMetadata.ResourceId => FileName ?? String.Empty;
    LocalDateTime IResourceMetadata.Modified { get; }

    Dictionary<string, LocalDateTime> IResourceMetadata.ModifiedSources { get; } = new Dictionary<string, LocalDateTime>(StringComparer.Ordinal) { { "Source1", LocalDateTime.FromDateTime(DateTime.UtcNow) } };

    object IResourceMetadata.Extensions => new
    {
        FileName,
    };
}