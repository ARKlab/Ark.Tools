// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace Ark.Tools.AspNetCore.CommaSeparatedParameters;

public class SeparatedQueryValueProviderFactory : IValueProviderFactory
{
    private readonly char _separator;
    private readonly string? _key;

    public SeparatedQueryValueProviderFactory(char separator) : this(null, separator)
    {
    }

    public SeparatedQueryValueProviderFactory(string? key, char separator)
    {
        _separator = separator;
        _key = key;
    }


    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        context.ValueProviders.Insert(0, new SeparatedQueryValueProvider(_key, _separator, context.ActionContext.HttpContext.Request.Query));
        return Task.CompletedTask;
    }
}