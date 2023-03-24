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

namespace WebApplicationDemo.Controllers.V1
{
    [ApiVersion(1.0)]
    public class MarketRecordController : ODataController
    {
        private List<MarketRecord> _values { get; init; }

        public MarketRecordController()
        {
            _values = new List<MarketRecord>();
            DateTimeOffset offset = DateTimeOffset
                .FromUnixTimeSeconds(1679353200);

            for(int i = 0; i < 48 ; i++)
            {
                _values.Add(new MarketRecord
                {
                    Market = "IT",
                    DateTimeOffset = offset,
                });

                offset = offset.AddSeconds(1);
            }
        }

        [HttpGet]
        [EnableQuery]
        public IQueryable<MarketRecord> Get()
        {
            return _values.AsQueryable();
        }

        [HttpGet]
        [EnableQuery]
        public SingleResult<MarketRecord> Get(
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
