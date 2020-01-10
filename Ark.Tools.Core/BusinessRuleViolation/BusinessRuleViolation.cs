using System.Collections.Generic;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    public class BusinessRuleViolation
    {
        public BusinessRuleViolation(string title)
        {
            Title = title;            
        }

		public int Status { get; set; }
		public string Title { get; set; }
        public string Detail { get; set; }
    }
}
