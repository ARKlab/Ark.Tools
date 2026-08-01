// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.AzureFunctions.Generators;

/// <summary>
/// Establishes the Azure Functions generator pipeline. Endpoint emission is
/// implemented by the subsequent trigger-generation task.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AzureFunctionsEndpointGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
