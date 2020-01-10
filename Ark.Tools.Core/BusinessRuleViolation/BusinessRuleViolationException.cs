using System;
using System.Collections.Generic;
using System.Text;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    public class BusinessRuleViolationException : Exception
    {
        public BusinessRuleViolationException(BusinessRuleViolation br): base(br.Title)
        {
            BusinessRuleViolation = br;
        }

        public BusinessRuleViolation BusinessRuleViolation { get; set; }


        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Title   : {BusinessRuleViolation.Title}");
            stringBuilder.AppendLine($"Details : {BusinessRuleViolation.Detail}");
            stringBuilder.AppendLine($"Status  : {BusinessRuleViolation.Status}");

            return stringBuilder.ToString();
        }
    }
}
