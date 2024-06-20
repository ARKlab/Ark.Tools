using Ark.Tools.Solid;

using Core.Service.Common.Dto;

namespace Core.Service.API.Queries
{
    public static class Ping_GetByNameQuery
    {
        public class V1 : IQuery<Ping.V1.Output>
        {
            public string Name { get; set; }
        }
    }
}
