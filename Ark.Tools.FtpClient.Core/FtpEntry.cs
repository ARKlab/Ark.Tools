// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.FtpClient.Core
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