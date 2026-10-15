// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters;

public sealed class SeparatedQueryValueProvider : QueryStringValueProvider
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

        if (result != ValueProviderResult.None && _containsSeparator(result.Values))
        {
            var splitValues = new StringValues(result.Values
                .SelectMany(x => x!.Split([_separator], StringSplitOptions.None)).ToArray());
            return new ValueProviderResult(splitValues, result.Culture);
        }

        return result;
    }

    private bool _containsSeparator(StringValues values)
    {
        foreach (var value in values)
        {
            if (value != null && value.AsSpan().IndexOf(_separator) > 0)
                return true;
        }
        return false;
    }
}