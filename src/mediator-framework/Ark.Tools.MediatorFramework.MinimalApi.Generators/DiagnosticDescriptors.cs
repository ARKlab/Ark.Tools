// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnknownHttpVerb = new(
        "ARKMF010", "Unknown HTTP verb", "HTTP endpoint '{0}' uses unsupported verb '{1}'",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedHandlerKind = new(
        "ARKMF011", "Unsupported handler kind", "Attributed type '{0}' does not implement a supported handler interface",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingRouteProperty = new(
        "ARKMF012", "Missing route property", "HTTP endpoint '{0}' has route placeholder '{1}' without a matching property",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidContractShape = new(
        "ARKMF013", "Invalid contract shape", "HTTP endpoint '{0}' must be a record with settable properties for body or multipart binding",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
