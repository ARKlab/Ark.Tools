using Ark.Tools.Core.BusinessRuleViolation;
using Hellang.Middleware.ProblemDetails;
using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class BusinessRuleProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        internal BusinessRuleViolation Violation { get; set; }


    }
}
