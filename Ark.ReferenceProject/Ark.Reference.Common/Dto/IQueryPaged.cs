using System.Collections.Generic;

namespace Ark.Reference.Common.Dto
{
    public interface IQueryPaged
    {
        IEnumerable<string> Sort { get; set; }
        int Limit { get; set; }
        int Skip { get; set; }
    }
}
