// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public class FtpFilter
    {
        public Predicate<string>? FolderFilter { get; set; }
        public Predicate<string>? FileFilter { get; set; }
        public string[] FoldersToWatch { get; set; } = ["./"]; 
    }
}
