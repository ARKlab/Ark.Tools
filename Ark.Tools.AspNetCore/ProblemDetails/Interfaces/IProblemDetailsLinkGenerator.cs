using Ark.Tools.Core.BusinessRuleViolation;
using Microsoft.AspNetCore.Http;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public interface IProblemDetailsLinkGenerator
    {
        string GetLink(ArkProblemDetails type, HttpContext ctx);

        string GetLink(BusinessRuleViolation type, HttpContext ctx);
    }
}
