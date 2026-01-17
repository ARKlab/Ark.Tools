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

    /// <summary>
    /// Finds a resource state by resource ID suffix.
    /// Common pattern used across multiple step classes for finding resources in loaded states.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="states">The collection of resource states to search.</param>
    /// <param name="resourceId">The resource ID or suffix to search for.</param>
    /// <returns>The matching resource state.</returns>
    public static ResourceState<TExtensions> FindByResourceId<TExtensions>(
        this IEnumerable<ResourceState<TExtensions>>? states,
        string resourceId)
        where TExtensions : class
    {
        states.Should().NotBeNull("Loaded states should not be null when finding resource");
        return states!.GetFirst(
            s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal),
            $"Resource with ID ending in '{resourceId}'");
    }

    /// <summary>
    /// Asserts that a resource state contains a specific resource by ID.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="states">The collection of resource states.</param>
    /// <param name="resourceId">The resource ID or suffix to check for.</param>
    public static void ShouldContainResource<TExtensions>(
        this IEnumerable<ResourceState<TExtensions>>? states,
        string resourceId)
        where TExtensions : class
    {
        states.Should().NotBeNull("Loaded states should not be null");
        states!.Should().Contain(
            s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal),
            $"Resource with ID ending in '{resourceId}' should exist");
    }

    /// <summary>
    /// Asserts that a resource state does not contain a specific resource by ID.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="states">The collection of resource states.</param>
    /// <param name="resourceId">The resource ID or suffix to check for.</param>
    public static void ShouldNotContainResource<TExtensions>(
        this IEnumerable<ResourceState<TExtensions>>? states,
        string resourceId)
        where TExtensions : class
    {
        states.Should().NotBeNull("Loaded states should not be null");
        states!.Should().NotContain(
            s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal),
            $"Resource with ID ending in '{resourceId}' should not exist");
    }

    /// <summary>
    /// Asserts the count of resources in a collection.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="states">The collection of resource states.</param>
    /// <param name="expectedCount">The expected number of resources.</param>
    public static void ShouldHaveResourceCount<TExtensions>(
        this IEnumerable<ResourceState<TExtensions>>? states,
        int expectedCount)
        where TExtensions : class
    {
        states.Should().NotBeNull("Loaded states should not be null");
        states!.Should().HaveCount(expectedCount, $"Should have {expectedCount} resources");
    }

    /// <summary>
    /// Sets a ModifiedSource on a resource state, handling dictionary initialization and clearing Modified.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="state">The resource state to modify.</param>
    /// <param name="sourceName">The source name to set.</param>
    /// <param name="modifiedDateTime">The modified date/time value.</param>
    public static void SetModifiedSource<TExtensions>(
        this ResourceState<TExtensions> state,
        string sourceName,
        LocalDateTime modifiedDateTime)
        where TExtensions : class
    {
        state.ModifiedSources ??= CreateModifiedSourcesDictionary();
        state.ModifiedSources[sourceName] = modifiedDateTime;
        // Clear Modified when using ModifiedSources
        state.Modified = default;
    }

    /// <summary>
    /// Sets a ModifiedSource from an ISO 8601 date/time string.
    /// </summary>
    /// <typeparam name="TExtensions">The extension type for the resource state.</typeparam>
    /// <param name="state">The resource state to modify.</param>
    /// <param name="sourceName">The source name to set.</param>
    /// <param name="modifiedString">The ISO 8601 formatted date/time string.</param>
    public static void SetModifiedSource<TExtensions>(
        this ResourceState<TExtensions> state,
        string sourceName,
        string modifiedString)
        where TExtensions : class
    {
        var modified = ParseLocalDateTime(modifiedString);
        state.SetModifiedSource(sourceName, modified);
    }
}