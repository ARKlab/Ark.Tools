// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Validates source-generated JSON metadata during host startup.</summary>
public static class MessagingJsonStartupValidation
{
    /// <summary>Validates one contract against host-configured JSON metadata.</summary>
    /// <typeparam name="T">The messaging contract type.</typeparam>
    /// <param name="options">The host JSON options.</param>
    public static void ValidateContract<T>(JsonSerializerOptions options) where T : class
    {
        ArgumentNullException.ThrowIfNull(options);
        _validate(options, typeof(T));
    }

    /// <summary>Validates all declared contracts against host-configured JSON metadata.</summary>
    /// <param name="options">The host JSON options.</param>
    /// <param name="contractTypes">The declared contract types.</param>
    public static void ValidateContracts(
        JsonSerializerOptions options,
        IEnumerable<Type> contractTypes)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contractTypes);
        foreach (var contractType in contractTypes)
        {
            ArgumentNullException.ThrowIfNull(contractType);
            _validate(options, contractType);
        }
    }

    private static void _validate(JsonSerializerOptions options, Type contractType)
    {
        try
        {
            _ = options.GetTypeInfo(contractType);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Contract '{contractType}' is not resolvable from the registered JsonSerializerOptions. "
                + "Register the shared contracts JsonSerializerContext on this host.",
                exception);
        }
    }
}
