using Ark.Tools.ResourceWatcher;

using NodaTime;


namespace TestWorker.Dto;

public class Test_FileMetadataDto : IResourceMetadata
{
    public string? FileName { get; set; }
    public LocalDate Date { get; set; }

    string IResourceMetadata<VoidExtensions>.ResourceId => FileName ?? String.Empty;
    LocalDateTime IResourceMetadata<VoidExtensions>.Modified { get; }

    Dictionary<string, LocalDateTime>? IResourceMetadata<VoidExtensions>.ModifiedSources { get; } = new Dictionary<string, LocalDateTime>(StringComparer.Ordinal) { { "Source1", LocalDateTime.FromDateTime(DateTime.UtcNow) } };

    VoidExtensions? IResourceMetadata<VoidExtensions>.Extensions => null;
}