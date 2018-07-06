using System;

namespace Ark.AspNetCore.CommaSeparatedParameters
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class CsvAttribute : Attribute
    {
    }
}
