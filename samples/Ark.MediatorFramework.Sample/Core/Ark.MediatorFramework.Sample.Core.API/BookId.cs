// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Vogen;

// protobuf-net serializes a Guid as a string from Level300, matching the exported .proto (ARKMF060).
[assembly: ProtoBuf.CompatibilityLevel(ProtoBuf.CompatibilityLevel.Level300)]

// Vogen implements IComparable so value objects can be sorted; ordering operators would add API nobody uses.
[assembly: SuppressMessage("Design", "MA0097:A class that implements IComparable<T> or IComparable should override comparison operators", Justification = "Vogen value objects are sortable through IComparable only.", Scope = "namespaceanddescendants", Target = "~N:Ark.MediatorFramework.Sample.Core.API")]

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>Identifies a book.</summary>
/// <remarks>
/// A Vogen value object: JSON, routes, query strings and OpenAPI see the wrapped <see cref="Guid"/>. Hosts register
/// it for Dapper with <c>ValueObjectDapper.Register</c>; gRPC registers it when the services are mapped.
/// </remarks>
[ValueObject<Guid>]
public readonly partial struct BookId
{
    /// <summary>Creates a new random book identifier.</summary>
    /// <returns>A new identifier.</returns>
    public static BookId New() => From(Guid.NewGuid());
}
