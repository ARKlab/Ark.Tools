using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Nodatime.Dapper
{
    public enum InstantHandlerType
    {
        DateTime = 0,
        Int64Ticks = 1,
        Int64Milliseconds = 2,
        Int64Seconds = 3,
    }
}
