// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Configures authentication behavior for generated Azure Functions endpoints.</summary>
public sealed class ArkAzureFunctionsAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the authentication scheme. <see langword="null"/> uses the host default scheme.
    /// </summary>
    public string? Scheme { get; set; }
}
