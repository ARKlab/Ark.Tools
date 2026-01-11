using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Ark.Tools.Outbox.SqlServer;

/// <summary>
/// Simple serializer that can be used to encode/decode headers to/from bytes
/// </summary>
public class HeaderSerializer
{
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        .ConfigureArkDefaults();

    static HeaderSerializer()
    {
        // ensure Dictionary Key are not mangled
        _options.DictionaryKeyPolicy = null;
    }

    /// <summary>
    /// Encodes the headers into a string
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The type Dictionary<string, string> is a well-known type with simple key-value pairs. Both string key and string value are primitive types that are always preserved by the trimmer.")]
    public string SerializeToString(Dictionary<string, string>? headers)
    {
        return JsonSerializer.Serialize(headers, _options);
    }

    /// <summary>
    /// Encodes the headers into a byte array
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The type Dictionary<string, string> is a well-known type with simple key-value pairs. Both string key and string value are primitive types that are always preserved by the trimmer.")]
    public byte[] Serialize(Dictionary<string, string>? headers)
    {
        return JsonSerializer.SerializeToUtf8Bytes(headers, _options);
    }

    /// <summary>
    /// Decodes the headers from the given byte array
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The type Dictionary<string, string> is a well-known type with simple key-value pairs. Both string key and string value are primitive types that are always preserved by the trimmer.")]
    public Dictionary<string, string>? Deserialize(byte[] bytes)
    {
        var readOnlySpan = new ReadOnlySpan<byte>(bytes);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(readOnlySpan, _options);
    }

    /// <summary>
    /// Decodes the headers from the given string
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "The type Dictionary<string, string> is a well-known type with simple key-value pairs. Both string key and string value are primitive types that are always preserved by the trimmer.")]
    public Dictionary<string, string>? DeserializeFromString(string str)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(str, _options);
    }
}