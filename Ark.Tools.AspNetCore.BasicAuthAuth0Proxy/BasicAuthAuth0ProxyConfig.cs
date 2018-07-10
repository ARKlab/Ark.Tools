namespace Ark.Tools.AspNetCore.BasicAuthAuth0Proxy
{
    public class BasicAuthAuth0ProxyConfig
    {
        public string Audience { get; set; }
        public string ProxyClientId { get; set; }
        public string Domain { get; set; }
        public string ProxySecret { get;  set; }
        public string Realm { get; set; }
    }
}