// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using ProtoBuf.Meta;

namespace Ark.Tools.Core.Protobuf;

/// <summary>
/// Registration helper wiring <see cref="EvolvableEnumSurrogate{TEnum}"/> into a protobuf-net
/// <see cref="RuntimeTypeModel"/>. protobuf-net has no support for resolving a surrogate registered
/// on an open generic type definition against arbitrary closed instantiations encountered later in
/// the object graph, so each wrapped enum type must be registered explicitly by calling
/// <see cref="AddEvolvableEnumSurrogate{TEnum}(RuntimeTypeModel)"/> once per <c>TEnum</c> — exactly
/// like registering a Dapper type handler.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Registers the <see cref="EvolvableEnumSurrogate{TEnum}"/> surrogate for the given wrapped
    /// enum type on the given protobuf-net model. Calling it more than once for the same
    /// <typeparamref name="TEnum"/> is a no-op if already registered.
    /// </summary>
    /// <typeparam name="TEnum">The wrapped enum type.</typeparam>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    public static RuntimeTypeModel AddEvolvableEnumSurrogate<TEnum>(this RuntimeTypeModel model)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!model.IsDefined(typeof(EvolvableEnum<TEnum>)))
            model.Add(typeof(EvolvableEnum<TEnum>), false).SetSurrogate(typeof(EvolvableEnumSurrogate<TEnum>));

        return model;
    }
}

