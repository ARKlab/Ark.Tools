using Asp.Versioning;
using Asp.Versioning.OData;

using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.OData.ModelBuilder;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Configuration
{
    public class MarketRecordConfiguration : IModelConfiguration
    {
        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            var record = builder.EntitySet<MarketRecord>("MarketRecord").EntityType;

            record.HasKey(p => new 
            { 
                p.Market, 
                p.DateTimeOffset 
            });

            record.Select().Filter();
        }
    }
}
