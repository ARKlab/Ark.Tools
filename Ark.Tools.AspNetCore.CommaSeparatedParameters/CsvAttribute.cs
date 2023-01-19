// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.AspNetCore.CommaSeparatedParameters
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class CsvAttribute : Attribute
    {
        public char Separator { get; set; } = ',';
    }
}
