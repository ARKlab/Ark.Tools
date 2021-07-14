using Ark.Tools.Core.BusinessRuleViolation;
using Hellang.Middleware.ProblemDetails;
using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class BusinessRuleProblemDetails : StatusCodeProblemDetails
    {
        internal BusinessRuleViolation Violation { get; set; }

        public BusinessRuleProblemDetails(BusinessRuleViolation businessRuleViolation) : base(businessRuleViolation.Status)
        {
            Title = businessRuleViolation.Title;
            Status = businessRuleViolation.Status;
            Detail = businessRuleViolation.Detail;

            Violation = businessRuleViolation;

            var type = businessRuleViolation.GetType();
            var properties = type.GetProperties().Where(p => p.DeclaringType.FullName != type.BaseType.FullName).ToArray();

            foreach (var prop in properties)
                Extensions.Add(prop.Name, prop.GetValue(businessRuleViolation, null));

            //foreach (var item in businessRuleViolation.Extensions)
            //    Extensions.Add(item);
        }

    }
}
