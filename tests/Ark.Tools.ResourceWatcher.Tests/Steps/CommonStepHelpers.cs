// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using AwesomeAssertions;

using NodaTime;
using NodaTime.Text;


namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Common helper methods shared across step definitions to reduce code duplication.
/// This follows the Driver pattern for Reqnroll tests.
/// </summary>
internal static class CommonStepHelpers
{
    /// <summary>
    /// Parses an ISO 8601 LocalDateTime string.
    /// </summary>
    /// <param name="dateTimeString">The ISO 8601 formatted date/time string.</param>
    /// <returns>The parsed LocalDateTime.</returns>
    public static LocalDateTime ParseLocalDateTime(string dateTimeString)
    {
        return LocalDateTimePattern.ExtendedIso.Parse(dateTimeString).Value;
    }

    /// <summary>
    /// Converts a LocalDateTime to an Instant in UTC.
    /// </summary>
    /// <param name="localDateTime">The LocalDateTime to convert.</param>
    /// <returns>The Instant in UTC.</returns>
    public static Instant ToInstant(LocalDateTime localDateTime)
    {
        return localDateTime.InUtc().ToInstant();
    }

    /// <summary>
    /// Parses an ISO 8601 LocalDateTime string and converts it to an Instant.
    /// </summary>
    /// <param name="dateTimeString">The ISO 8601 formatted date/time string.</param>
    /// <returns>The parsed Instant in UTC.</returns>
    public static Instant ParseInstant(string dateTimeString)
    {
        return ToInstant(ParseLocalDateTime(dateTimeString));
    }

    /// <summary>
    /// Creates a dictionary for ModifiedSources with case-insensitive key comparison.
    /// </summary>
    /// <returns>A new dictionary with case-insensitive string keys.</returns>
    public static Dictionary<string, LocalDateTime> CreateModifiedSourcesDictionary()
    {
        return new Dictionary<string, LocalDateTime>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a source name to lowercase for consistent storage.
    /// </summary>
    /// <param name="sourceName">The source name to normalize.</param>
    /// <returns>The normalized source name in lowercase.</returns>
    public static string NormalizeSourceName(string sourceName)
    {
        return sourceName.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the first item matching a predicate with a helpful assertion message.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="predicate">The predicate to match.</param>
    /// <param name="itemDescription">Description for the assertion message.</param>
    /// <returns>The first matching item.</returns>
    public static T GetFirst<T>(this IEnumerable<T> source, Func<T, bool> predicate, string itemDescription)
    {
        var item = source.FirstOrDefault(predicate);
        item.Should().NotBeNull($"{itemDescription} should exist");
        return item!;
    }
}