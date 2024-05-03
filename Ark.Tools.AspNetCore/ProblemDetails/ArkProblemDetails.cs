// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public abstract class ArkProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        public ArkProblemDetails(string title)
        {
            Title = title;            
        }
    }
}
