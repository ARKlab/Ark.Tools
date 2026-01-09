using System;
using System.Collections.Generic;
using System.Text.Json;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox.SqlServer(net10.0)', Before:
namespace Ark.Tools.Outbox.SqlServer
{
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
        public string SerializeToString(Dictionary<string, string>? headers)
        {
            return JsonSerializer.Serialize(headers, _options);
        }

        /// <summary>
        /// Encodes the headers into a byte array
        /// </summary>
        public byte[] Serialize(Dictionary<string, string>? headers)
        {
            return JsonSerializer.SerializeToUtf8Bytes(headers, _options);
        }

        /// <summary>
        /// Decodes the headers from the given byte array
        /// </summary>
        public Dictionary<string, string>? Deserialize(byte[] bytes)
        {
            var readOnlySpan = new ReadOnlySpan<byte>(bytes);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(readOnlySpan, _options);
        }

        /// <summary>
        /// Decodes the headers from the given string
        /// </summary>
        public Dictionary<string, string>? DeserializeFromString(string str)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(str, _options);
        }
=======
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
    public string SerializeToString(Dictionary<string, string>? headers)
    {
        return JsonSerializer.Serialize(headers, _options);
    }

    /// <summary>
    /// Encodes the headers into a byte array
    /// </summary>
    public byte[] Serialize(Dictionary<string, string>? headers)
    {
        return JsonSerializer.SerializeToUtf8Bytes(headers, _options);
    }

    /// <summary>
    /// Decodes the headers from the given byte array
    /// </summary>
    public Dictionary<string, string>? Deserialize(byte[] bytes)
    {
        var readOnlySpan = new ReadOnlySpan<byte>(bytes);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(readOnlySpan, _options);
    }

    /// <summary>
    /// Decodes the headers from the given string
    /// </summary>
    public Dictionary<string, string>? DeserializeFromString(string str)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(str, _options);
>>>>>>> After


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
    public string SerializeToString(Dictionary<string, string>? headers)
    {
        return JsonSerializer.Serialize(headers, _options);
    }

    /// <summary>
    /// Encodes the headers into a byte array
    /// </summary>
    public byte[] Serialize(Dictionary<string, string>? headers)
    {
        return JsonSerializer.SerializeToUtf8Bytes(headers, _options);
    }

    /// <summary>
    /// Decodes the headers from the given byte array
    /// </summary>
    public Dictionary<string, string>? Deserialize(byte[] bytes)
    {
        var readOnlySpan = new ReadOnlySpan<byte>(bytes);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(readOnlySpan, _options);
    }

    /// <summary>
    /// Decodes the headers from the given string
    /// </summary>
    public Dictionary<string, string>? DeserializeFromString(string str)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(str, _options);
    }
}