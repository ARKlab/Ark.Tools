using System.IO;

namespace Core.Service.Tests
{
    public class FileData
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string Path { get; set; }
        public Stream Stream { get; set; }
    }
}