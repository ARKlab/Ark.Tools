using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.HealthChecks
{
    public class HealthCheckDto
    {
        public string Status { get; set; }
        public string Component { get; set; }
        public string Description { get; set; }
        public string Exception { get; set; }
    }
}
