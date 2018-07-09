namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy
{
    public class BasicAuthAzureActiveDirectoryProxyConfig
    {
        public string Tenant { get; set; }
        public string Resource { get; set; }
        public string ProxyClientId { get; set; }
        public string ProxyClientSecret { get; set; }
    }
}