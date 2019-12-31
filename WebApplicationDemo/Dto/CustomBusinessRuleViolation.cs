using Ark.Tools.Core.BusinessRuleViolation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
