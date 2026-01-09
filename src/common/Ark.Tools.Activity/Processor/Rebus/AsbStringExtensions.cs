using System.Globalization;
using System.Linq;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
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
                .Select(c => char.IsLetterOrDigit(c) || c == '/' ? char.ToLower(c, CultureInfo.InvariantCulture) : '_'));
        }
=======
namespace Ark.Tools.Activity;

public static class AsbStringExtensions
{
    /// <summary>
    /// Gets a valid topic name from the given topic string. This conversion is a one-way destructive conversion!
    /// </summary>
    public static string ToValidAzureServiceBusEntityName(this string topic)
    {
        return string.Concat(topic
            .Select(c => char.IsLetterOrDigit(c) || c == '/' ? char.ToLower(c, CultureInfo.InvariantCulture) : '_'));
>>>>>>> After


namespace Ark.Tools.Activity;

    public static class AsbStringExtensions
    {
        /// <summary>
        /// Gets a valid topic name from the given topic string. This conversion is a one-way destructive conversion!
        /// </summary>
        public static string ToValidAzureServiceBusEntityName(this string topic)
        {
            return string.Concat(topic
                .Select(c => char.IsLetterOrDigit(c) || c == '/' ? char.ToLower(c, CultureInfo.InvariantCulture) : '_'));
        }
    }