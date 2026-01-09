using Ark.Tools.Solid;

using System.Collections.Generic;

namespace Ark.Reference.Core.API.Queries;

public static class Audit_GetUsersQuery
{
    public record V1 : IQuery<IEnumerable<string>>
    {
    }
}