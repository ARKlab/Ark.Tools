using Ark.Tools.ResourceWatcher;
using NodaTime;
using System;
using System.Collections.Generic;

namespace TestWorker.Dto
{
    public class Test_FileMetadataDto : IResourceMetadata
    {
        public string FileName { get; set; }
        public LocalDate Date { get; set; }

        string IResourceMetadata.ResourceId => FileName;
        LocalDateTime? IResourceMetadata.Modified { get; } = LocalDateTime.FromDateTime(DateTime.UtcNow);

        Dictionary<string, LocalDateTime> IResourceMetadata.ModifiedMultiple { get; } = new Dictionary<string, LocalDateTime>{ { "Key1",  LocalDateTime.FromDateTime(DateTime.UtcNow)} };

        object IResourceMetadata.Extensions => new
        {
            FileName,
        };
    }

}
