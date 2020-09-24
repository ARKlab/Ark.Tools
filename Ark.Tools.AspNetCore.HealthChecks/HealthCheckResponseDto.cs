using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.HealthChecks
{
    public class HealthCheckResponseDto
    {
        public string Status { get; set; }

        public IEnumerable<HealthCheckDto> Checks { get; set; }

        public TimeSpan Duration { get; set; }
    }
}
