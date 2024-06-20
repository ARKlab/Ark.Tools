using Ark.Tools.Solid;

using Core.Service.Common.Dto;

namespace Core.Service.API.Queries
{
    public static class Ping_GetByIdQuery
    {
        public class V1 : IQuery<Ping.V1.Output>
        {
            public int Id { get; set; }
        }
    }
}
