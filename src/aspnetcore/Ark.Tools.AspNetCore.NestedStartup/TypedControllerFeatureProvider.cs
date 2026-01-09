// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Controllers;

using System.Reflection;

namespace Ark.Tools.AspNetCore.NestedStartup;

public class TypedControllerFeatureProvider<TArea> : ControllerFeatureProvider where TArea : IArea
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        var ret = false;
        if (!typeof(IArea<TArea>).GetTypeInfo().IsAssignableFrom(typeInfo)) ret = false;
        else ret = base.IsController(typeInfo);

        return ret;
    }
}
public interface IArea { }

public interface IArea<T> where T : IArea { }