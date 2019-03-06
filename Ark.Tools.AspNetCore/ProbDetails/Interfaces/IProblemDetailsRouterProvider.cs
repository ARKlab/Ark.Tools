using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public interface IProblemDetailsRouterProvider
    {
        IRouter Router { get; }
        IRouter BuildRouter(IApplicationBuilder app);
    }
}
