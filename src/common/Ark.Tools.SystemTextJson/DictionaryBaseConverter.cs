using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Ark.Tools.SystemTextJson;

public sealed class DictionaryBaseConverter<TC, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TK, TV> : AbstractDictionaryConverter<TC, TK, TV>
    where TK : notnull
    where TC : Dictionary<TK, TV>, new()
{
    public DictionaryBaseConverter(JsonSerializerOptions options)
        : base(options)
    {
    }

    protected override TC InstantiateCollection(IDictionary<TK, TV> workingCollection)
    {
        return (TC)workingCollection;
    }

    protected override IDictionary<TK, TV> InstantiateWorkingCollection()
    {
        return new TC();
    }
}

public sealed class IDictionaryBaseConverter<TC, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TK, TV> : AbstractDictionaryConverter<TC, TK, TV>
    where TK : notnull
    where TC : IDictionary<TK, TV>, new()
{
    public IDictionaryBaseConverter(JsonSerializerOptions options)
        : base(options)
    {
    }

    protected override TC InstantiateCollection(IDictionary<TK, TV> workingCollection)
    {
        return (TC)workingCollection;
    }

    protected override IDictionary<TK, TV> InstantiateWorkingCollection()
    {
        return new TC();
    }
}

public sealed class DictionaryConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TK, TV>
    : AbstractDictionaryConverter<IDictionary<TK, TV>, TK, TV>
    where TK : notnull
{
    public DictionaryConverter(JsonSerializerOptions options) : base(options)
    {
    }

    protected override IDictionary<TK, TV> InstantiateCollection(IDictionary<TK, TV> workingCollection)
    {
        return workingCollection;
    }

    protected override IDictionary<TK, TV> InstantiateWorkingCollection()
    {
        return new Dictionary<TK, TV>();
    }
}

public sealed class ReadOnlyDictionaryConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TK, TV>
    : AbstractDictionaryConverter<IReadOnlyDictionary<TK, TV>, TK, TV>
    where TK : notnull
{
    public ReadOnlyDictionaryConverter(JsonSerializerOptions options)
        : base(options)
    {
    }

    protected override IReadOnlyDictionary<TK, TV> InstantiateCollection(IDictionary<TK, TV> workingCollection)
    {
        return new ReadOnlyDictionary<TK, TV>(workingCollection);
    }

    protected override IDictionary<TK, TV> InstantiateWorkingCollection()
    {
        return new Dictionary<TK, TV>();
    }
}