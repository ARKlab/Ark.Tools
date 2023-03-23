using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
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
        public IEnumerable<MarketRecord> Get(ODataQueryOptions<MarketRecord> query)
        {
            return ((IQueryable<MarketRecord>)query.ApplyTo(_values.AsQueryable())).ToArray();
        }

        [HttpGet]
        [EnableQuery]
        public IActionResult Get(
            [FromODataUri] DateTimeOffset keyDateTimeOffset,
            [FromODataUri] string keyMarket)
        {
            var record = _values
                .Where(r => r.Market == keyMarket &&
                            r.DateTimeOffset == keyDateTimeOffset)
                .FirstOrDefault();

            return Ok(record);
        }
    }
}
