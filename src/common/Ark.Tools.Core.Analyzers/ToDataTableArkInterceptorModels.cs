// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.Core.Analyzers;

// Equatable, symbol-free data models carried across the incremental generator pipeline boundary for
// ToDataTableArkInterceptorGenerator. Keeping these free of ISymbol/SyntaxNode references lets the
// incremental pipeline compare and (in principle) cache values across compilations by value.

/// <summary>The kind of per-value conversion a single shredded member requires, mirroring the runtime fallback's ConvertColumnValue rules.</summary>
internal enum ConversionKind
{
    /// <summary>No conversion: the value (or its boxed Nullable&lt;T&gt; underlying value) is used as-is.</summary>
    Direct,

    /// <summary>Enum members are converted via their ToString() member name.</summary>
    EnumToString,

    /// <summary>EvolvableEnum values are converted via their ToString() representation.</summary>
    EvolvableEnumToString,

    /// <summary>NodaTime LocalDate/LocalDateTime converted via ToDateTimeUnspecified().</summary>
    LocalDateToDateTime,

    /// <summary>NodaTime LocalDateTime converted via ToDateTimeUnspecified().</summary>
    LocalDateTimeToDateTime,

    /// <summary>NodaTime Instant converted via ToDateTimeUtc().</summary>
    InstantToDateTime,

    /// <summary>NodaTime OffsetDateTime converted via ToDateTimeOffset().</summary>
    OffsetDateTimeToDateTimeOffset,

    /// <summary>NodaTime OffsetDate converted via At(LocalTime.Midnight).ToDateTimeOffset().</summary>
    OffsetDateToDateTimeOffset,

    /// <summary>NodaTime LocalTime converted via TimeSpan.FromTicks(TickOfDay).</summary>
    LocalTimeToTimeSpan,

    /// <summary>Sensitive value objects (Ark.Tools.Compliance.ISensitiveValue&lt;TSelf&gt;) converted to their cleartext transport string via SensitiveValueSerialization.ToTransport.</summary>
    SensitiveValueToTransport,
}

/// <summary>A single shredded column: its source member name, nullability, conversion, and derived DataColumn type.</summary>
internal readonly record struct MemberModel(string Name, bool IsNullable, ConversionKind Conversion, string ColumnTypeFullName);

/// <summary>The cached shape of a compile-time-known element type T eligible for interception.</summary>
internal readonly record struct TypeModel(
    string FullyQualifiedName,
    string SimpleName,
    bool IsReferenceType,
    bool IsPrimitiveScalar,
    ImmutableArray<MemberModel> Members);

/// <summary>A single ToDataTableArk() call site: the element type it was called with, and its unique interceptable source location.</summary>
internal readonly record struct CallSiteModel(TypeModel Type, InterceptableLocation Location);

internal sealed class CallSiteModelComparer : IEqualityComparer<CallSiteModel>
{
    internal static readonly CallSiteModelComparer _instance = new();

    public bool Equals(CallSiteModel x, CallSiteModel y)
    {
        return _equals(x, y);
    }

    internal sealed class CallSiteModelArrayComparer : IEqualityComparer<ImmutableArray<CallSiteModel>>
    {
        internal static readonly CallSiteModelArrayComparer _instance = new();

        public bool Equals(ImmutableArray<CallSiteModel> x, ImmutableArray<CallSiteModel> y)
        {
            if (x.Length != y.Length)
                return false;

            for (var i = 0; i < x.Length; i++)
            {
                if (!CallSiteModelComparer._instance.Equals(x[i], y[i]))
                    return false;
            }

            return true;
        }

        public int GetHashCode(ImmutableArray<CallSiteModel> obj)
        {
            var hash = 17;
            foreach (var item in obj)
                hash = (hash * 397) ^ CallSiteModelComparer._instance.GetHashCode(item);

            return hash;
        }
    }

    public int GetHashCode(CallSiteModel obj)
    {
        return _getHashCode(obj);
    }

    private static bool _equals(CallSiteModel x, CallSiteModel y)
    {
        return x.Location.Version == y.Location.Version
            && string.Equals(x.Location.Data, y.Location.Data, StringComparison.Ordinal)
            && string.Equals(x.Type.FullyQualifiedName, y.Type.FullyQualifiedName, StringComparison.Ordinal)
            && string.Equals(x.Type.SimpleName, y.Type.SimpleName, StringComparison.Ordinal)
            && x.Type.IsReferenceType == y.Type.IsReferenceType
            && x.Type.IsPrimitiveScalar == y.Type.IsPrimitiveScalar
            && x.Type.Members.SequenceEqual(y.Type.Members);
    }

    private static int _getHashCode(CallSiteModel obj)
    {
        var hash = StringComparer.Ordinal.GetHashCode(obj.Location.Data);
        hash = (hash * 397) ^ obj.Location.Version;
        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.Type.FullyQualifiedName);
        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.Type.SimpleName);
        hash = (hash * 397) ^ obj.Type.IsReferenceType.GetHashCode();
        hash = (hash * 397) ^ obj.Type.IsPrimitiveScalar.GetHashCode();
        foreach (var member in obj.Type.Members)
        {
            hash = (hash * 397) ^ member.GetHashCode();
        }

        return hash;
    }
}

internal static class SymbolExtensions
{
    /// <summary>Returns the fully-qualified (global::-prefixed) display name for use directly after <c>typeof(</c> or in a type position.</summary>
    public static string ToFullyQualifiedString(this ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// Determines whether a struct type corresponds to a CLR "primitive" type (<see cref="System.Type.IsPrimitive"/>),
    /// which the reflection fallback shreds as a single scalar "Value" column rather than by its members.
    /// </summary>
    public static bool IsPrimitiveScalar(this ITypeSymbol type) => type.SpecialType is
        SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
        SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
        SpecialType.System_Char or SpecialType.System_Double or SpecialType.System_Single or
        SpecialType.System_IntPtr or SpecialType.System_UIntPtr;
}
