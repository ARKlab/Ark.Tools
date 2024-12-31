// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194:Implement exception constructors.", Justification = "Created from BusinessRuleViolation")]
    public sealed class BusinessRuleViolationException : Exception
    {
        public BusinessRuleViolationException(BusinessRuleViolation br)
            : base(br.Detail)
        {
            Data.Add("BusinessRuleViolation", br);
        }

        public BusinessRuleViolationException(BusinessRuleViolation br, Exception innerException)
            : base(br.Detail, innerException)
        {
            Data.Add("BusinessRuleViolation", br);
        }

        public BusinessRuleViolation BusinessRuleViolation
        {
            get => (Data["BusinessRuleViolation"] as BusinessRuleViolation)!;
        }

    }


}
