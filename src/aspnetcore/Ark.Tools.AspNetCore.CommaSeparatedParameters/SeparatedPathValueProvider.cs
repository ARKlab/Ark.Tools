// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

using System.Buffers;
using System.Globalization;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters;

public class SeparatedPathValueProvider : RouteValueProvider
{
    private readonly SearchValues<char> _separatorSearchValues;
    private readonly char _separator;
    private readonly string? _key;

    public SeparatedPathValueProvider(char separator, RouteValueDictionary values)
        : this(null, separator, values)
    { }

    public SeparatedPathValueProvider(string? key, char separator, RouteValueDictionary values)
        : base(BindingSource.Path, values, CultureInfo.InvariantCulture)
    {
        _separator = separator;
        _separatorSearchValues = SearchValues.Create([separator]);
        _key = key;
    }

    public override ValueProviderResult GetValue(string key)
    {
        if (_key != null && _key != key)
        {
            return ValueProviderResult.None;
        }

        var result = base.GetValue(key);

        if (result != ValueProviderResult.None && result.Values.Any(x => x != null && x.AsSpan().IndexOfAny(_separatorSearchValues) >= 0))
        {
            var splitValues = new List<string>();
            foreach (var value in result.Values)
            {
                if (value == null)
                    continue;

                var span = value.AsSpan();
                if (span.IndexOfAny(_separatorSearchValues) < 0)
                {
                    splitValues.Add(value);
                    continue;
                }

                // Use Span-based splitting to avoid allocations
                int start = 0;
                int separatorIndex;
                while ((separatorIndex = span[start..].IndexOf(_separator)) >= 0)
                {
                    splitValues.Add(span.Slice(start, separatorIndex).ToString());
                    start += separatorIndex + 1;
                }

                // Add the remaining part
                if (start < span.Length)
                {
                    splitValues.Add(span[start..].ToString());
                }
                else if (start == span.Length)
                {
                    // Handle trailing separator
                    splitValues.Add(string.Empty);
                }
            }

            return new ValueProviderResult(new StringValues([.. splitValues]), result.Culture);
        }

        return result;
    }
}