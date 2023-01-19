// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using System;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters
{
    public class SeparatedQueryValueProvider : QueryStringValueProvider
    {
        private readonly char _separator;
        private readonly string? _key;

        public SeparatedQueryValueProvider(char separator, IQueryCollection values) 
            : this(null, separator, values)
        { }

        public SeparatedQueryValueProvider(string? key, char separator, IQueryCollection values) 
            : base(BindingSource.Query, values, CultureInfo.InvariantCulture)
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

            if (result != ValueProviderResult.None && result.Values.Any(x => x.IndexOf(_separator) > 0))
            {
                var splitValues = new StringValues(result.Values
                    .SelectMany(x => x.Split(new[] { _separator }, StringSplitOptions.None)).ToArray());
                return new ValueProviderResult(splitValues, result.Culture);
            }

            return result;
        }
    }
}
