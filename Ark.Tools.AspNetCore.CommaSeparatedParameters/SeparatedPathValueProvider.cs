// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters
{
    public class SeparatedPathValueProvider : RouteValueProvider
    {
        private readonly char _separator;
        private readonly string? _key;

        public SeparatedPathValueProvider(char separator, RouteValueDictionary values) 
            : this(null, separator, values)
        { }

        public SeparatedPathValueProvider(string? key, char separator, RouteValueDictionary values) 
            : base(BindingSource.Path, values, CultureInfo.InvariantCulture)
        {
            _separator = separator;
            _key = key;
        }

        public override ValueProviderResult GetValue(string key)
        {
            if (_key != null && _key != key)
            {
                return ValueProviderResult.None;
            }

            var result = base.GetValue(key);

            if (result != ValueProviderResult.None && result.Values.Any(x => x?.IndexOf(_separator) > 0))
            {
                var splitValues = new StringValues(result.Values
                    .SelectMany(x => x!.Split([_separator], StringSplitOptions.None)).ToArray());
                return new ValueProviderResult(splitValues, result.Culture);
            }

            return result;
        }
    }
}
