// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>
/// Registers value objects, such as Vogen types, on a protobuf-net <see cref="RuntimeTypeModel"/> so they are
/// serialized as the protobuf scalar of the primitive they wrap. A <see cref="Guid"/> is written as its canonical
/// string, matching protobuf-net's <c>CompatibilityLevel.Level300</c> and the exported <c>.proto</c>.
/// </summary>
/// <remarks>
/// The generated <c>MapArkGrpcServicesFromAssembly</c> registers every value object reachable from the served
/// contracts; call this directly only for contracts serialized outside the generated services.
/// </remarks>
public static class ArkProtobufValueObjects
{
    /// <summary>
    /// Registers a value object as a protobuf scalar. Calling it more than once is a no-op for an already-registered type.
    /// </summary>
    /// <remarks>
    /// protobuf-net creates the serializer from its type, so a value object has one pair of conversions per process:
    /// the first registration's <paramref name="from"/> and <paramref name="value"/> serve every model.
    /// </remarks>
    /// <typeparam name="TValueObject">The value object.</typeparam>
    /// <typeparam name="TPrimitive">
    /// The wrapped primitive: <see cref="string"/>, <see cref="Guid"/>, <see cref="bool"/>, <see cref="int"/>,
    /// <see cref="long"/>, <see cref="float"/> or <see cref="double"/>.
    /// </typeparam>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <param name="from">Creates the value object from its primitive, validating it.</param>
    /// <param name="value">Reads the primitive of a value object.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    /// <exception cref="NotSupportedException"><typeparamref name="TPrimitive"/> has no protobuf scalar.</exception>
    public static RuntimeTypeModel Register<TValueObject, TPrimitive>(
        RuntimeTypeModel model,
        Func<TPrimitive, TValueObject> from,
        Func<TValueObject, TPrimitive> value)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(value);

        lock (model)
        {
            ValueObjectSerializer<TValueObject, TPrimitive>._configure(from, value);
            // Add returns the existing MetaType of a type already known to the model.
            var metaType = model.Add(typeof(TValueObject), applyDefaultBehaviour: false);
            if (metaType.SerializerType is null)
                metaType.SerializerType = typeof(ValueObjectSerializer<TValueObject, TPrimitive>);
        }

        return model;
    }
}

/// <summary>Serializes a value object as the protobuf scalar of its primitive.</summary>
/// <typeparam name="TValueObject">The value object.</typeparam>
/// <typeparam name="TPrimitive">The wrapped primitive.</typeparam>
internal sealed class ValueObjectSerializer<TValueObject, TPrimitive> : ISerializer<TValueObject>
{
    // protobuf-net creates the serializer from its type, so the conversions are held per closed generic type.
    // Both delegates are published as one object so a serializer never mixes the conversions of two registrations.
    private static Conversions? _conversions;

    private static readonly SerializerFeatures? _features = typeof(TPrimitive) switch
    {
        var t when t == typeof(string) || t == typeof(Guid) => SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeString,
        var t when t == typeof(bool) || t == typeof(int) || t == typeof(long) => SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeVarint,
        var t when t == typeof(float) => SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeFixed32,
        var t when t == typeof(double) => SerializerFeatures.CategoryScalar | SerializerFeatures.WireTypeFixed64,
        _ => default(SerializerFeatures?),
    };

    internal static void _configure(Func<TPrimitive, TValueObject> from, Func<TValueObject, TPrimitive> value)
    {
        if (_features is null)
            throw new NotSupportedException($"Value object '{typeof(TValueObject)}' wraps '{typeof(TPrimitive)}', which has no protobuf scalar.");

        // The first registration wins, so a later one, even on another model, cannot change existing serialization.
        Interlocked.CompareExchange(ref _conversions, new Conversions(from, value), null);
    }

    private sealed record Conversions(Func<TPrimitive, TValueObject> From, Func<TValueObject, TPrimitive> Value);

    /// <inheritdoc />
    public SerializerFeatures Features => _features!.Value;

    /// <inheritdoc />
    public TValueObject Read(ref ProtoReader.State state, TValueObject value)
    {
        object primitive = typeof(TPrimitive) switch
        {
            var t when t == typeof(string) => state.ReadString(),
            var t when t == typeof(Guid) => Guid.Parse(state.ReadString(), System.Globalization.CultureInfo.InvariantCulture),
            var t when t == typeof(bool) => state.ReadBoolean(),
            var t when t == typeof(int) => state.ReadInt32(),
            var t when t == typeof(long) => state.ReadInt64(),
            var t when t == typeof(float) => state.ReadSingle(),
            _ => state.ReadDouble(),
        };
        return _conversions!.From((TPrimitive)primitive);
    }

    /// <inheritdoc />
    public void Write(ref ProtoWriter.State state, TValueObject value)
    {
        object? primitive = _conversions!.Value(value);
        switch (primitive)
        {
            case string text:
                state.WriteString(text);
                break;
            case Guid guid:
                state.WriteString(guid.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case bool flag:
                state.WriteBoolean(flag);
                break;
            case int number:
                state.WriteInt32(number);
                break;
            case long number:
                state.WriteInt64(number);
                break;
            case float number:
                state.WriteSingle(number);
                break;
            case double number:
                state.WriteDouble(number);
                break;
            default:
                throw new InvalidOperationException($"Value object '{typeof(TValueObject)}' has no primitive value.");
        }
    }
}
