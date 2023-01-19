// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    public class BusinessRuleViolation
    {
        public BusinessRuleViolation(string title)
        {
            Title = title;            
        }

        public int Status { get; set; } = 400;
		public string Title { get; set; }
        public string? Detail { get; set; }
    }
}
