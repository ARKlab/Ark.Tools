using Ark.Tools.Core.BusinessRuleViolation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ProblemDetailsLinkGenerator : IProblemDetailsLinkGenerator
    {
        private IProblemDetailsRouterProvider _provider;

        public ProblemDetailsLinkGenerator(IProblemDetailsRouterProvider provider)
        {
            _provider = provider;
        }

        public string GetLink(ArkProblemDetails type, HttpContext ctx)
        {
            var dictionary = new RouteValueDictionary
                {
                    { "name" , $"{type.GetType().AssemblyQualifiedName}" }
                };
            var av = ctx?.Features.Get<IRouteValuesFeature>()?.RouteValues;
            var path = _provider.Router.GetVirtualPath(new VirtualPathContext(ctx, av, dictionary, "ProblemDetails"));

            var link = UriHelper.BuildRelative(ctx.Request.PathBase, path.VirtualPath);
            return link;
        }

        public string GetLink(BusinessRuleViolation type, HttpContext ctx)
        {
            var dictionary = new RouteValueDictionary
                {
                    { "name" , $"{type.GetType().AssemblyQualifiedName}" }
                };
            var av = ctx?.Features.Get<IRouteValuesFeature>()?.RouteValues;
            var path = _provider.Router.GetVirtualPath(new VirtualPathContext(ctx, av, dictionary, "ProblemDetails"));

            var link = UriHelper.BuildRelative(ctx.Request.PathBase, path.VirtualPath);
            return link;
        }
    }
}
