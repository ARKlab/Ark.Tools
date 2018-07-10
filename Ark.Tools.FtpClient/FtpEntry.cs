// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
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