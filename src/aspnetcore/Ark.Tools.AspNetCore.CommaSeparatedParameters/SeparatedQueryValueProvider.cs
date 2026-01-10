// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

using System.Buffers;
using System.Globalization;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters;

public class SeparatedQueryValueProvider : QueryStringValueProvider
{
    private readonly SearchValues<char> _separatorSearchValues;
    private readonly char _separator;
    private readonly string? _key;

    public SeparatedQueryValueProvider(char separator, IQueryCollection values)
        : this(null, separator, values)
    { }

    public SeparatedQueryValueProvider(string? key, char separator, IQueryCollection values)
        : base(BindingSource.Query, values, CultureInfo.InvariantCulture)
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
            // Use Span-based splitting via extension method from Ark.Tools.Core
            // Reduces intermediate allocations during parsing compared to string.Split()
            // Final StringValues conversion still allocates, but separator detection and slicing are allocation-free
            var splitValues = result.Values
                .Where(x => x != null)
                .SelectMany(x => x!.SplitToList(_separator))
                .ToArray();

            return new ValueProviderResult(new StringValues(splitValues), result.Culture);
        }

        return result;
    }
}