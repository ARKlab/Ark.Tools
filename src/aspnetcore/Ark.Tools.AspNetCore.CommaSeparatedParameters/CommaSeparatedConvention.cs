// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using System;
using System.Collections;
using System.Linq;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters
{

    public class CommaSeparatedConvention : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            foreach (var parameter in action.Parameters)
            {
                if (_isArrayOrCollection(parameter.ParameterInfo.ParameterType))
                {
                    var attr = parameter.Attributes.OfType<CsvAttribute>().FirstOrDefault();
                    if (attr != null)
                    {
                        if (parameter.BindingInfo?.BindingSource == BindingSource.Path)
                            parameter.Action.Filters.Add(new SeparatedPathValueAttribute(parameter.ParameterName, attr.Separator));
                        if (parameter.BindingInfo?.BindingSource == BindingSource.Query)
                            parameter.Action.Filters.Add(new SeparatedQueryValueAttribute(parameter.ParameterName, attr.Separator));
                    }
                }
            }
        }

        private static bool _isArrayOrCollection(Type type)
        {
            return type.IsArray || typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}