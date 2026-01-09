using Ark.Tools.Solid;


namespace Ark.Reference.Core.API.Queries;

public static class Audit_GetUsersQuery
{
    public record V1 : IQuery<IEnumerable<string>>
    {
    }
}