using System.Net;

namespace Ark.Tools.FtpClient
{
    public interface IFtpClientFactory
    {
        IFtpClient Create(string host, NetworkCredential credentials);
    }
}