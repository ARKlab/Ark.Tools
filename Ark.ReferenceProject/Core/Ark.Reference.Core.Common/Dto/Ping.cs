using Ark.Reference.Core.Common.Enum;

using Ark.Reference.Common.Services.Audit;

using System;

namespace Ark.Reference.Core.Common.Dto
{
    public static class Ping
    {
        public static class V1
        {
            public record Create 
            {
                public string? Name { get; init; }
                public PingType? Type { get; init; }
            }

            public record Update : Create
            {
                public int Id { get; init; }
            }

            public record Output : Update, IAuditEntity
            {
                public string? Code { get; init; }
                public Guid AuditId { get; init; }
            }
        }
    }
}
