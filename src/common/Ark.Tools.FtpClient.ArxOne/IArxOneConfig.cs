using Ark.Tools.FtpClient.Core;

using ArxOne.Ftp;

using System;

namespace Ark.Tools.FtpClient;

public interface IArxOneConfig
{
    ISocksConfig? SocksConfig { get; }
    Action<FtpClientParameters>? Configurer { get; }
}
