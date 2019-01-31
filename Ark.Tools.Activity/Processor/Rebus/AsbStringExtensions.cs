using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Activity
{
    public static class AsbStringExtensions
    {
        /// <summary>
        /// Gets a valid topic name from the given topic string. This conversion is a one-way destructive conversion!
        /// </summary>
        public static string ToValidAzureServiceBusEntityName(this string topic)
        {
            return string.Concat(topic
                .Select(c => char.IsLetterOrDigit(c) || c == '/' ? char.ToLower(c) : '_'));
        }
    }
}
