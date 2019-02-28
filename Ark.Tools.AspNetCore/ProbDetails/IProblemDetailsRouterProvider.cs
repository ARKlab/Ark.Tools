using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public interface IProblemDetailsRouterProvider
    {
        IRouter Router { get; }
        IRouter BuildRouter(IApplicationBuilder app);
    }
}
