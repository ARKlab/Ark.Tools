using Ark.Tools.FtpClient.Core;

using ArxOne.Ftp;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.ArxOne(net10.0)', Before:
namespace Ark.Tools.FtpClient
{
    public interface IArxOneConfig
    {
        ISocksConfig? SocksConfig { get; }
        Action<FtpClientParameters>? Configurer { get; }
    }


=======
namespace Ark.Tools.FtpClient;

public interface IArxOneConfig
{
    ISocksConfig? SocksConfig { get; }
    Action<FtpClientParameters>? Configurer { get; }
>>>>>>> After
    namespace Ark.Tools.FtpClient;

    public interface IArxOneConfig
    {
        ISocksConfig? SocksConfig { get; }
        Action<FtpClientParameters>? Configurer { get; }
    }