using System.Collections.Generic;

namespace Ark.Reference.Core.API.Auth
{
    public static class AuthScopes
    {
        public const string AuditRead = "audit:read";


        public static readonly IReadOnlyDictionary<string, string> Scopes = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            { AuthScopes.AuditRead, "Audit Read" },
        };

        public static IEnumerable<string> All => Scopes.Keys;
    }
}