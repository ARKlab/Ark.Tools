
namespace Ark.Reference.Common.Dto.Queries;

public static class AuditChangesQueryDto
{
    public record V1
    {
        public Guid AuditID { get; set; }
    }
}