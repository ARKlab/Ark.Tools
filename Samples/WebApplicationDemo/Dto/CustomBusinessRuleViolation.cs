using Ark.Tools.Core.BusinessRuleViolation;

using System.Collections.Generic;

namespace WebApplicationDemo.Dto
{
    public class CustomBusinessRuleViolation : BusinessRuleViolation
    {
        public CustomBusinessRuleViolation()
            : base("Custom Business Rule Violation Title!!")
        {
            Accounts = new List<string>();
        }

        public decimal Balance { get; set; }

        public ICollection<string> Accounts { get; }
    }
}
