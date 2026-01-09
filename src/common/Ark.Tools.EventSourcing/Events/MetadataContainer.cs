
namespace Ark.Tools.EventSourcing.Events;

public class MetadataContainer : Dictionary<string, string>
{
    public MetadataContainer()
    {
    }

    public MetadataContainer(IDictionary<string, string> keyValuePairs)
        : base(keyValuePairs)
    {
    }

    public MetadataContainer(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        : base(keyValuePairs.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal))
    {
    }

    public MetadataContainer(params KeyValuePair<string, string>[] keyValuePairs)
        : this((IEnumerable<KeyValuePair<string, string>>)keyValuePairs)
    {
    }

    public void AddRange(params KeyValuePair<string, string>[] keyValuePairs)
    {
        AddRange((IEnumerable<KeyValuePair<string, string>>)keyValuePairs);
    }

    public void AddRange(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
    {
        foreach (var keyValuePair in keyValuePairs)
        {
            Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, this.Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    public string? GetMetadataValue(string key)
    {
        return GetMetadataValue(key, s => s);
    }

    public T? GetMetadataValue<T>(string key, Func<string, T> converter, T? defaultValue = null)
        where T : struct
    {
        if (!TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value is null) return null;

        return converter(value);
    }

    public T? GetMetadataValue<T>(string key, Func<string, T> converter, T? defaultValue = null)
        where T : class
    {
        if (!TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value is null) return null;

        return converter(value);
    }

    public void SetMetadataValue<T>(string key, T? value, Func<T, string> converter)
        where T : struct
    {
        if (!value.HasValue)
        {
            Remove(key);
        }
        else
        {
            this[key] = converter(value.Value);
        }
    }

    public void SetMetadataValue<T>(string key, T? value, Func<T, string> converter)
        where T : class
    {
        if (value is null)
        {
            Remove(key);
        }
        else
        {
            this[key] = converter(value);
        }
    }

    public void SetMetadataValue(string key, string? value)
    {
        if (value is null)
        {
            Remove(key);
        }
        else
        {
            this[key] = value;
        }
    }
}