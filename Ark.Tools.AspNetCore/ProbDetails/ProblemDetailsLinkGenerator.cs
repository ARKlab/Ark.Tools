using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Ark.Tools.AspNetCore.ProbDetails
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
                    { "name" , type.GetType().Name }
                };
            var av = ctx?.Features.Get<IRouteValuesFeature>()?.RouteValues;
            var path = _provider.Router.GetVirtualPath(new VirtualPathContext(ctx, av, dictionary, "ProblemDetails"));

            var link = UriHelper.BuildAbsolute(ctx.Request.Scheme, ctx.Request.Host, ctx.Request.PathBase, path.VirtualPath);
            return link;
        }
    }
}
