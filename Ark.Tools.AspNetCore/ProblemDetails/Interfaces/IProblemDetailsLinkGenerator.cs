// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core.BusinessRuleViolation;

using Microsoft.AspNetCore.Http;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public interface IProblemDetailsLinkGenerator
    {
        string GetLink(ArkProblemDetails type, HttpContext ctx);

        string GetLink(BusinessRuleViolation type, HttpContext ctx);
    }
}
