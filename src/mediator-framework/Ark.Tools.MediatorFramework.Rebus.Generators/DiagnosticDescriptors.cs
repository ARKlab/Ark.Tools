// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnsupportedHandlerKind = new(
        "ARKMF011", "Unsupported handler kind", "Attributed type '{0}' does not implement a supported handler interface",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateRegistration = new(
        "ARKMF014", "Duplicate Rebus registration", "Rebus contract '{0}' is registered more than once",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingOwnerQueue = new(
        "ARKMF015", "Conflicting Rebus owner queue", "Rebus contract '{0}' has conflicting owner queues: '{1}' and '{2}'",
        "Ark.MediatorFramework", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
