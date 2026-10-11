// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    // Each registered value object over its primitive's empty value, which proto3 never writes, and its
    // uninitialized default.
    private static readonly ConcurrentDictionary<Type, (Func<object?> Empty, object? Default)> _emptyValues = new();

    // The primitive each value object is registered over: one wire format per value object, as for its conversions.
    private static readonly ConcurrentDictionary<Type, Type> _primitives = new();

    // The contracts each model already creates through AbsentValueObjects: protobuf-net freezes a type once it
    // serialized it, so a second call, such as from another host sharing RuntimeTypeModel.Default, must not change it.
    private static readonly ConditionalWeakTable<RuntimeTypeModel, HashSet<Type>> _initializedContracts = new();

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
    /// <exception cref="InvalidOperationException">The value object is already registered over another primitive.</exception>
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
            // Rejects a primitive without a protobuf scalar before the value object is bound to it.
            ValueObjectSerializer<TValueObject, TPrimitive>._configure(from, value);
            var primitive = _primitives.GetOrAdd(typeof(TValueObject), typeof(TPrimitive));
            if (primitive != typeof(TPrimitive))
                throw new InvalidOperationException($"Value object '{typeof(TValueObject)}' is already registered over '{primitive}', not '{typeof(TPrimitive)}'.");
            // Through the serializer's published conversions, so absent members use the same From as reads.
            _emptyValues.TryAdd(typeof(TValueObject), (
                static () => ValueObjectSerializer<TValueObject, TPrimitive>._from(string.Empty is TPrimitive empty ? empty : default!),
                default(TValueObject)));
            // Add returns the existing MetaType of a type already known to the model.
            var metaType = model.Add(typeof(TValueObject), applyDefaultBehaviour: false);
            if (metaType.SerializerType is null)
                metaType.SerializerType = typeof(ValueObjectSerializer<TValueObject, TPrimitive>);
        }

        return model;
    }

    /// <summary>
    /// Sets the value object members a message leaves absent to the value object over the primitive's empty value,
    /// such as <c>""</c> or <c>0</c>, as JSON and MessagePack read an explicit empty value.
    /// </summary>
    /// <remarks>
    /// proto3 never writes a scalar's empty value, so an absent field is how a client sends <c>""</c> or <c>0</c>;
    /// without this, the member keeps its uninitialized default. A member that an initializer already set keeps its
    /// value. protobuf-net creates the contract from its type, so every model shares one set of members: calling it
    /// again with the same members is a no-op, and with different members throws. The generated
    /// <c>MapArkGrpcServicesFromAssembly</c> calls this for the served contracts.
    /// </remarks>
    /// <typeparam name="TContract">The protobuf contract.</typeparam>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <param name="members">The settable properties of a value object type registered with <see cref="Register{TValueObject, TPrimitive}"/>.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    /// <exception cref="ArgumentException">A member is not a settable property of a registered value object type.</exception>
    /// <exception cref="InvalidOperationException">The contract is already configured with different members.</exception>
    public static RuntimeTypeModel InitializeAbsentValueObjects<[DynamicallyAccessedMembers(AbsentValueObjects._contractMembers)] TContract>(
        RuntimeTypeModel model,
        params string[] members)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(members);

        var properties = new AbsentMember[members.Length];
        for (var i = 0; i < members.Length; i++)
        {
            var property = typeof(TContract).GetProperty(members[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.SetMethod is null || !_emptyValues.TryGetValue(property.PropertyType, out var value))
                throw new ArgumentException($"'{typeof(TContract)}.{members[i]}' is not a settable property of a registered value object type.", nameof(members));
            properties[i] = new AbsentMember(property, value.Empty, value.Default);
        }

        lock (model)
        {
            AbsentValueObjects<TContract>._configure(properties);
            if (!_initializedContracts.GetOrCreateValue(model).Add(typeof(TContract)))
                return model;

            model.Add(typeof(TContract), applyDefaultBehaviour: true)
                .SetFactory(typeof(AbsentValueObjects<TContract>).GetMethod(nameof(AbsentValueObjects<TContract>.Create))!);
        }

        return model;
    }
}

/// <summary>A value object member to set when a message leaves it absent.</summary>
/// <param name="Property">The member.</param>
/// <param name="Empty">Creates the value object over the primitive's empty value.</param>
/// <param name="Default">The member's uninitialized value: the boxed default of a struct, or null.</param>
internal sealed record AbsentMember(PropertyInfo Property, Func<object?> Empty, object? Default);

/// <summary>Trimming annotations shared by <see cref="AbsentValueObjects{TContract}"/>.</summary>
internal static class AbsentValueObjects
{
    // The members InitializeAbsentValueObjects reads: the properties to set and the constructor to call.
    internal const DynamicallyAccessedMemberTypes _contractMembers = DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.NonPublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties;
}

/// <summary>Creates a contract whose absent value object members hold the value over the primitive's empty value.</summary>
/// <typeparam name="TContract">The protobuf contract.</typeparam>
internal static class AbsentValueObjects<[DynamicallyAccessedMembers(AbsentValueObjects._contractMembers)] TContract>
{
    // protobuf-net calls the factory from its type, so the members are held per closed generic type; the first
    // registration wins, as for the value object conversions.
    private static AbsentMember[]? _members;

    // The constructor protobuf-net itself would call; without one, it would skip construction.
    private static readonly ConstructorInfo? _constructor = typeof(TContract).GetConstructor(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);

    internal static void _configure(AbsentMember[] members)
    {
        var configured = Interlocked.CompareExchange(ref _members, members, null) ?? members;
        if (!configured.Select(static member => member.Property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(members.Select(static member => member.Property.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException($"The absent value object members of '{typeof(TContract)}' are already configured as {string.Join(", ", configured.Select(static member => member.Property.Name))}; every model shares them.");
    }

    /// <summary>Creates the contract before protobuf-net reads its fields, which then replace the preset members.</summary>
    /// <returns>The new contract.</returns>
    public static TContract Create()
    {
        var contract = _constructor is null
            ? RuntimeHelpers.GetUninitializedObject(typeof(TContract))
            : _constructor.Invoke(null);
        foreach (var member in _members!)
        {
            // Compared bitwise, because a value object's own Equals may throw when uninitialized.
            var current = member.Property.GetValue(contract);
            if (current is null || (member.Default is not null && RuntimeHelpers.Equals(current, member.Default)))
                member.Property.SetValue(contract, member.Empty());
        }

        return (TContract)contract;
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

    // The value object over a primitive, through the first registration's conversions.
    internal static TValueObject _from(TPrimitive primitive) => _conversions!.From(primitive);

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
            // An unset string value object (VOGEN_NO_VALIDATION) has a null value: written as the empty string, which
            // is how proto3 carries an absent string, as JSON writes null.
            case null when typeof(TPrimitive) == typeof(string):
                state.WriteString(string.Empty);
                break;
            default:
                throw new InvalidOperationException($"Value object '{typeof(TValueObject)}' has no primitive value.");
        }
    }
}
