using Microsoft.Extensions.DependencyInjection;

namespace Ark.AspNetCore.CommaSeparatedParameters
{
    public static class Ex
    {
        public static IMvcBuilder AddCommaSeparatedValues(this IMvcBuilder builder)
        {
            return builder.AddMvcOptions(o => o.Conventions.Add(new CommaSeparatedConvention()));
        }
    }
}
