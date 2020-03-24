using Ark.Tools.ResourceWatcher;
using NodaTime;
using System;
using System.Globalization;

namespace TestWorker.Dto
{
    public class Test_FileMetadataDto : IResourceMetadata
    {
        public string FileName { get; set; }
        public LocalDate Date { get; set; }

        string IResourceMetadata.ResourceId => FileName;
        LocalDateTime IResourceMetadata.Modified { get; } = LocalDateTime.FromDateTime(DateTime.UtcNow);

        object IResourceMetadata.Extensions => new
        {
            FileName,
        };
    }

}
