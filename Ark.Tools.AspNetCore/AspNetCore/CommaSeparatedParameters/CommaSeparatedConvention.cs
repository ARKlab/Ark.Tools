using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections;
using System.Linq;

namespace Ark.AspNetCore.CommaSeparatedParameters
{

    public class CommaSeparatedConvention : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            foreach (var parameter in action.Parameters)
            {
                if (_isArrayOrCollection(parameter.ParameterInfo.ParameterType)
                    && parameter.Attributes.OfType<CsvAttribute>().Any())
                {
                    if (parameter.BindingInfo?.BindingSource == BindingSource.Path)
                        parameter.Action.Filters.Add(new SeparatedPathValueAttribute(parameter.ParameterName, ','));
                    if (parameter.BindingInfo?.BindingSource == BindingSource.Query)
                        parameter.Action.Filters.Add(new SeparatedQueryValueAttribute(parameter.ParameterName, ','));
                }
            }
        }

        private static bool _isArrayOrCollection(Type type)
        {
            return type.IsArray || typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
