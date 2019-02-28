using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public interface IProblemDetailsLinkGenerator
    {
        string GetLink(ArkProblemDetails type, HttpContext ctx);
    }
}
