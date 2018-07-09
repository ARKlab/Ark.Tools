using System.Security.Claims;

namespace Ark.Tools.AspNetCore.Auth0
{
    public static class Auth0ClaimTypes
    {
        public const string Group = "http://schemas.xmlsoap.org/claims/Group";        
        public const string Role = ClaimTypes.Role;

        public const string Scope = "http://ark-energy.eu/claims/Scope";
        public const string Permission = "http://ark-energy.eu/claims/Permission";
        public const string PolicyPostPayload = "http://ark-energy.eu/claims/policyPostPayload";
    }
}
