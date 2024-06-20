using Core.Service.Common.Enum;

using Ark.Reference.Common.Services.Audit;

using System;

namespace Core.Service.Common.Dto
{
    public static class Ping
    {
        public static class V1
        {
            public class Create 
            {
                public string Name { get; set; }
                public PingType? Type { get; set; }
            }

            public class Update: Create
            {
                public int Id { get; set; }
            }

            public class Output : Update, IAuditEntity
            {
                public string Code { get; set; }
                public Guid AuditId { get; set; }
            }
        }
    }
}
