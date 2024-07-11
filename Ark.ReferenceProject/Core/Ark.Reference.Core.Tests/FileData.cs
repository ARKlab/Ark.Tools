using System.IO;

namespace Ark.Reference.Core.Tests
{
    public class FileData
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string Path { get; set; }
        public Stream Stream { get; set; }
    }
}