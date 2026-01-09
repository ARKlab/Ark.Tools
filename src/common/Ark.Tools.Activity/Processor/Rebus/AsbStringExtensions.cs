using System.Buffers;
using System.Globalization;

namespace Ark.Tools.Activity;

public static class AsbStringExtensions
{
    // SearchValues for valid Azure Service Bus entity name characters
    // Includes: A-Z, a-z, 0-9, and forward slash (/)
    // This explicit character list enables SIMD vectorization for optimal performance
    private static readonly SearchValues<char> _validChars = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/");

    /// <summary>
    /// Gets a valid topic name from the given topic string. This conversion is a one-way destructive conversion!
    /// </summary>
    public static string ToValidAzureServiceBusEntityName(this string topic)
    {
        return string.Concat(topic
            .Select(c => _validChars.Contains(c) ? char.ToLower(c, CultureInfo.InvariantCulture) : '_'));
    }
}