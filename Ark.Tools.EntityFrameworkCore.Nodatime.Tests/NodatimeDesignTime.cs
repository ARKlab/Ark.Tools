using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Ark.Tools.EntityFrameworkCore.Nodatime.Tests
{
    public class NodatimeDesignTime : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
        {
            serviceCollection.SetNodaTimeSqlServerMappingSource();
            //Debugger.Launch();
        }
    }
}
