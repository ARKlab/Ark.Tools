// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Solid.SimpleInjector;

/// <summary>
/// Opts a request, query, or command into source-generated SimpleInjector dispatch.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateSolidSimpleInjectorDispatchAttribute : Attribute
{
}
