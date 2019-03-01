using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public interface IProblemDetailsRouterProvider
    {
        IRouter Router { get; }
        IRouter BuildRouter(IApplicationBuilder app);
    }
}
