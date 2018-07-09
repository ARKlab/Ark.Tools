using EnsureThat;
using System.Net;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FtpClientFluentFtpFactory : IFtpClientFactory
    {
        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FtpClientFluentFtp(host, credentials);
        }
    }
}
