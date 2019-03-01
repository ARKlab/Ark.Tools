using Microsoft.AspNetCore.Http;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public interface IProblemDetailsLinkGenerator
    {
        string GetLink(ArkProblemDetails type, HttpContext ctx);
    }
}
