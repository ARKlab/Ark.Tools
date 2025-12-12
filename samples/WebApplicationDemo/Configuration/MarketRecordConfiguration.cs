using Asp.Versioning;
using Asp.Versioning.OData;

using Microsoft.OData.ModelBuilder;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Configuration
{
    public class MarketRecordConfiguration : IModelConfiguration
    {
        public void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            if (apiVersion == ApiVersions.V1)
            {
                var recordv1 = builder.EntitySet<MarketRecordV1>("MarketRecordV1").EntityType;

                recordv1.HasKey(p => new
                {
                    p.Market,
                    p.DateTimeOffset
                });

                recordv1.Select().Filter();
            }

            if (apiVersion == ApiVersions.V0)
            {
                var recordv0 = builder.EntitySet<MarketRecordV0>("MarketRecord").EntityType;

                recordv0.HasKey(p => new
                {
                    p.Market,
                    p.DateTimeOffset
                });
                recordv0.Select().Filter();
            }
        }
    }
}
