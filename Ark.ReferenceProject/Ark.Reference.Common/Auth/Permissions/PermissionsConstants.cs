using System.Collections.Generic;

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

        public static readonly Dictionary<string, Permissions> PermissionsMap = new()
        {
             { AdminGrant, Permissions.Admin }
        };
    }
}
