// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>
/// Converts route and query string values for generated Functions with the same strategies the Minimal API host
/// uses, so a contract binds the same way on both hosts.
/// </summary>
public static class ArkAzureFunctionsBinding
{
    /// <summary>Parses a value of a type that implements <see cref="IParsable{TSelf}"/>, explicitly or not.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The string value.</param>
    /// <param name="result">The parsed value when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse<T>(string? value, [MaybeNullWhen(false)] out T result)
        where T : IParsable<T>
    {
        return T.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}
