// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ApiBehaviourOptionsSetup : IConfigureOptions<ApiBehaviorOptions>
    {
        public void Configure(ApiBehaviorOptions options)
        {
            options.SuppressMapClientErrors = true;
        }
    }
}