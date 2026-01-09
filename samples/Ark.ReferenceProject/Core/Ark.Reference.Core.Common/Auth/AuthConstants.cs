namespace Ark.Reference.Core.Common.Auth
{

    public static class AuthConstants
    {
        //public const string ClaimScope = "scope";
        public const string ClaimRole = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

        public const string IntegrationTestsAudience = "API";
        public const string IntegrationTestsDomain = "local.dev";
        public const string IntegrationTestsEncryptionKey = "IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLongVeryLong";

        public const string ScopePrefix = "extension_Scope";

        public const string EntraIdSchema = "EntraId";

        public const string AzureAdSchema = "AzureAd";
        public const string AzureAdB2CSchema = "AzureAdB2C";

        public const string AzureB2CConfigSection = "AzureAdB2C";
    }
}