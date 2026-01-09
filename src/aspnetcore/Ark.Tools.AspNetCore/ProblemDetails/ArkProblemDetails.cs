// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.AspNetCore.ProblemDetails;

public abstract class ArkProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
{
    protected ArkProblemDetails(string title)
    {
        Title = title;
    }
}