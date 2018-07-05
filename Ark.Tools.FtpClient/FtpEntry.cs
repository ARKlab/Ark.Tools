using System;

namespace Ark.Tools.FtpClient
{
    public struct FtpEntry
    {
        //public ListStyle Style;
        public string Name;
        public string FullPath;
        public DateTime Modified;
        public bool IsDirectory;
        public long Size;
    }
}