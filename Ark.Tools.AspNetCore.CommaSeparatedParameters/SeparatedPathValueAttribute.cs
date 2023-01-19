// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class SeparatedPathValueAttribute : Attribute, IResourceFilter
    {
        private readonly SeparatedPathValueProviderFactory _factory;

        public SeparatedPathValueAttribute() : this(',')
        {
        }

        public SeparatedPathValueAttribute(char separator) 
            : this(null, separator)
        {
        }

        public SeparatedPathValueAttribute(string? key, char separator)
        {
            _factory = new SeparatedPathValueProviderFactory(key, separator);
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            context.ValueProviderFactories.Insert(0, _factory);
        }
    }
}
