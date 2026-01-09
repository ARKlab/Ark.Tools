using Asp.Versioning;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;



using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Controllers.V1;

[ApiVersion(1.0)]
public class MarketRecordV1Controller : ODataController
{
    private List<MarketRecordV1> _values { get; init; }

    public MarketRecordV1Controller()
    {
        _values = new List<MarketRecordV1>();
        DateTimeOffset offset = DateTimeOffset
            .FromUnixTimeSeconds(1679353200);

        for (int i = 0; i < 48; i++)
        {
            _values.Add(new MarketRecordV1
            {
                Market = "IT",
                DateTimeOffset = offset,
            });

            offset = offset.AddSeconds(1);
        }
    }

    [EnableQuery]
    public IQueryable<MarketRecordV1> Get()
    {
        return _values.AsQueryable();
    }

    [EnableQuery]
    public SingleResult<MarketRecordV1> Get(
        DateTimeOffset keydateTimeOffset,  // format is key{nameOfTheProperty in camelCase}
        string keymarket)
    {
        var record = _values
            .Where(r => r.Market == keymarket &&
                        r.DateTimeOffset == keydateTimeOffset)
            ;

        return SingleResult.Create(record.AsQueryable());
    }
}