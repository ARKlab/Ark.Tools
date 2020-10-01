using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplicationDemo.Dto
{
    public interface IExampleHealthCheckService
    {
        public Task CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}
