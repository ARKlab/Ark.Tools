using System;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public class FtpFilter
    {
        public Predicate<string> FolderFilter { get; set; }
        public Predicate<string> FileFilter { get; set; }
        public string[] FoldersToWatch { get; set; }
    }
}
