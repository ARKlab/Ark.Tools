using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ark.Reference.Common.Auth
{
    public enum Permissions
    {
        Admin,
    }

    public static class PermissionsConstants
    {
        public static readonly string PermissionKey = "extension_Scope";

        public static readonly string AdminGrant = "grant:admin";

        public static readonly IReadOnlyDictionary<string, Permissions> PermissionsMap = new Dictionary<string, Permissions>()
        {
             { AdminGrant, Permissions.Admin }
        }.ToImmutableDictionary();
    }
}
