using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    public class BusinessRuleViolationException : Exception
    {
        public BusinessRuleViolationException(BusinessRuleViolation br) : base(br.Detail)
        {
            BusinessRuleViolation = br;
        }
        public BusinessRuleViolationException(BusinessRuleViolation br, Exception innerException) : base(br.Detail, innerException)
        {
        }

        public BusinessRuleViolation BusinessRuleViolation { get; set; }
    }
}
