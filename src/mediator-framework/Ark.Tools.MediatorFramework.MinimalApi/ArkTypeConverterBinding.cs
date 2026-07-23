// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;

using Microsoft.AspNetCore.Http;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Converts route and query values using registered <see cref="TypeConverter"/> instances.</summary>
public static class ArkTypeConverterBinding
{
    /// <summary>Converts an invariant string value to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="value">The route or query value.</param>
    /// <param name="name">The route or query parameter name.</param>
    /// <returns>The converted value, or <see langword="default"/> when the value is absent.</returns>
    /// <exception cref="BadHttpRequestException">Thrown when the value cannot be converted.</exception>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The generator emits this method only for types with a TypeConverterAttribute, and the type parameter preserves the converter metadata.")]
    public static T? Convert<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] T>(string? value, string name)
    {
        if (value is null)
            return default;

        try
        {
            return (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException)
        {
            throw new BadHttpRequestException($"The '{name}' value is invalid.", StatusCodes.Status400BadRequest);
        }
    }
}
