// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters;

public static class Ex
{
    public static IMvcBuilder AddCommaSeparatedValues(this IMvcBuilder builder)
    {
        return builder.AddMvcOptions(o => o.Conventions.Add(new CommaSeparatedConvention()));
    }

    public static IMvcCoreBuilder AddCommaSeparatedValues(this IMvcCoreBuilder builder)
    {
        return builder.AddMvcOptions(o => o.Conventions.Add(new CommaSeparatedConvention()));
    }
}