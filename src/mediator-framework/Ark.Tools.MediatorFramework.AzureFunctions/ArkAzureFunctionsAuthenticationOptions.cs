// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Identifies the authentication profile used by an Azure Functions host.</summary>
public enum ArkAzureFunctionsAuthenticationProfile
{
    /// <summary>Use the registered ASP.NET Core authentication service, normally bearer authentication.</summary>
    DirectBearer,

    /// <summary>Use a separately configured trusted Easy Auth integration.</summary>
    EasyAuth,
}

/// <summary>Configures authentication behavior for generated Azure Functions endpoints.</summary>
public sealed class ArkAzureFunctionsAuthenticationOptions
{
    /// <summary>Gets or sets the authentication profile.</summary>
    public ArkAzureFunctionsAuthenticationProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the authentication scheme. <see langword="null"/> uses the host default scheme.
    /// </summary>
    public string? Scheme { get; set; }
}
