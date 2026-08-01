// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Dapper;

using Ark.Tools.Core;

namespace Ark.Tools.Dapper;

/// <summary>
/// Registration helper for evolvable enum type handlers. Dapper's type-handler
/// registry is keyed by exact closed type, so each wrapped enum type must be registered explicitly
/// (there is no open-generic registration for <see cref="EvolvableEnum{TEnum}"/>).
/// </summary>
public static class EvolvableEnumDapper
{
    /// <summary>
    /// Registers Dapper support for <see cref="EvolvableEnum{TEnum}"/> columns and parameters of the
    /// given wrapped enum type.
    /// </summary>
    /// <typeparam name="TEnum">The wrapped enum type.</typeparam>
    /// <param name="format">The SQL wire format: symbolic name (default) or numeric value.</param>
    public static void Register<TEnum>(EvolvableEnumWireFormat format = EvolvableEnumWireFormat.Name)
        where TEnum : struct, Enum
        => SqlMapper.AddTypeHandler(new EvolvableEnumTypeHandler<TEnum>(format));

    /// <summary>Registers Dapper support using the enum's exact integral backing type.</summary>
    /// <typeparam name="TEnum">The wrapped enum type.</typeparam>
    /// <typeparam name="TBacking">The enum's exact integral backing type.</typeparam>
    /// <param name="format">The SQL wire format: symbolic name (default) or numeric value.</param>
    public static void Register<TEnum, TBacking>(
        EvolvableEnumWireFormat format = EvolvableEnumWireFormat.Name)
        where TEnum : struct, Enum
        where TBacking : struct, System.Numerics.IBinaryInteger<TBacking>
        => SqlMapper.AddTypeHandler(new EvolvableEnumTypeHandler<TEnum, TBacking>(format));
}
