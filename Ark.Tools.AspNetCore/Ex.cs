using Ark.Tools.Solid.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore
{
    public static partial class Ex    
    {
        public static void RegisterAuthorizationAspNetCoreUser(this Container container, IApplicationBuilder app)
        {
            container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new AspNetCoreUserContextProvider(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>()));
        }
    }
}
