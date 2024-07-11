using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Ark.Reference.Common
{
    public class ArkKeyVaultSecretManager : KeyVaultSecretManager
    {
        public override string GetKey(KeyVaultSecret secret)
        {
            return base.GetKey(secret).Replace('-', '.');
        }
    }
}
