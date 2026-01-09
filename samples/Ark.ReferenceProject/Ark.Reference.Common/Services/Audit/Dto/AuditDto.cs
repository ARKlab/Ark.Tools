using NodaTime;


namespace Ark.Reference.Common.Services.Audit;

public class AuditDto<TAuditKind>
    where TAuditKind : struct, Enum
{
    public Guid AuditId { get; set; }
    public string? UserId { get; set; }
    public TAuditKind Kind { get; set; }
    public string? Info { get; set; }
    public Instant SysStartTime { get; set; }
    public Instant SysEndTime { get; set; }
}