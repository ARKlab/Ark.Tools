using NodaTime;
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.Sql;


namespace Ark.Reference.Common.Services.Audit;

[SqlDataPolicy(Schema = "dbo", Table = "Audit", Label = "Personal Data")]
public class AuditDto<TAuditKind>
    where TAuditKind : struct, Enum
{
    public Guid AuditId { get; set; }
    [Pseudonymous]
    [SqlColumnPolicy("UserId", StoragePolicy.Masked, InformationType = "User Identifier")]
    public string? UserId { get; set; }
    public TAuditKind Kind { get; set; }
    public string? Info { get; set; }
    public Instant SysStartTime { get; set; }
    public Instant SysEndTime { get; set; }
}