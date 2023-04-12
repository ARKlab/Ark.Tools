using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

using System;
using System.Collections.Generic;

using System.Linq;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Controllers.V0
{
    [ApiVersion(0.0)]
    public class MarketRecordController : ODataController
    {
        private List<MarketRecordV0> _values { get; init; }

        public MarketRecordController()
        {
            _values = new List<MarketRecordV0>();
            DateTimeOffset offset = DateTimeOffset
                .FromUnixTimeSeconds(1679353200);

            for(int i = 0; i < 48 ; i++)
            {
                _values.Add(new MarketRecordV0
                {
                    Market = "IT",
                    DateTimeOffset = offset,
                });

                offset = offset.AddSeconds(1);
            }
        }

        [HttpGet]
        [EnableQuery]
        public IQueryable<MarketRecordV0> Get()
        {
            return _values.AsQueryable();
        }

        [HttpGet]
        [EnableQuery]
        public SingleResult<MarketRecordV0> Get(
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
}
