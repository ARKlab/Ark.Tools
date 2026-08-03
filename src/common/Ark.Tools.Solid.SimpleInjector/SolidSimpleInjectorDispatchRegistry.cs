// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Solid.SimpleInjector;

/// <summary>
/// Stores the generated dispatcher discovered in the consuming application.
/// </summary>
public static class SolidSimpleInjectorDispatchRegistry
{
    /// <summary>
    /// Gets or sets the generated dispatcher, when one was emitted for the application.
    /// </summary>
    public static ISolidSimpleInjectorDispatcher? Current { get; set; }
}
