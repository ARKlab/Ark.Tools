using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public class ActivityEnrichmentOptions
    {

        public HashSet<string> HostNames { get; set; }

        public string[] Headers { get; set; }
    }
}
