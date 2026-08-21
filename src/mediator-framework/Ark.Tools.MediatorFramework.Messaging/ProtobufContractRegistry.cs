// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Stores the generated parser delegate for one protobuf contract.</summary>
/// <typeparam name="T">The protobuf contract type.</typeparam>
public static class ProtobufContractRegistry<T> where T : class
{
    private static Func<ReadOnlySequence<byte>, T>? _parse;

    /// <summary>Gets or sets the parser delegate.</summary>
    public static Func<ReadOnlySequence<byte>, T>? Parse
    {
        get
        {
            return Volatile.Read(ref _parse);
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Interlocked.CompareExchange(ref _parse, value, null) is not null)
                throw new InvalidOperationException(
                    $"A protobuf parser is already registered for contract '{typeof(T)}'.");
        }
    }
}
