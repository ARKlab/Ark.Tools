using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using System;

namespace Microsoft.Extensions.Configuration
{
    public static class KeyVaultConfigurationExtensions
    {
        public class ArkKeyVaultSecretManager : KeyVaultSecretManager
        {
            public override string GetKey(KeyVaultSecret secret)
            {
                return base.GetKey(secret).Replace('-', '.');
            }
        }

        /// <summary>
        /// AddAzureKeyVaultMSI
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IConfigurationBuilder AddAzureKeyVaultMSI(this IConfigurationBuilder builder)
        {
            var config = builder.Build();
            var keyVaultBaseUrl = config["KeyVault:BaseUrl"];

            if (!string.IsNullOrEmpty(keyVaultBaseUrl))
                builder.AddAzureKeyVault(
                    new Uri(keyVaultBaseUrl)
                    , new DefaultAzureCredential()
                    , new ArkKeyVaultSecretManager()
                );

            return builder;
        }
    }
}
