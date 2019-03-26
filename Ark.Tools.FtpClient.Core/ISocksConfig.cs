using Org.Mentalis.Network.ProxySocket;

namespace Ark.Tools.FtpClient.Core
{
    public interface ISocksConfig
    {
        string IpAddress { get; }
        int Port { get; }
        string UserName { get; }
        string Password { get; }
        ProxyTypes Type { get; }
    }
}