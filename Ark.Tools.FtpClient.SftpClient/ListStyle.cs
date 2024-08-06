// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using EnsureThat;
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.SftpClient
{

    internal enum ListStyle
    {
        Unix,
        Windows
    }

}
