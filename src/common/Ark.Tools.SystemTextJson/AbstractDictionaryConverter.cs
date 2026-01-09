using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.SystemTextJson(net10.0)', Before:
namespace Ark.Tools.SystemTextJson
{
    public abstract class AbstractDictionaryConverter<TC, TK, TV> : JsonConverter<TC>
        where TK : notnull
        where TC : IEnumerable<KeyValuePair<TK, TV>>
    {
        private readonly TypeConverter _keyConverter;
        private readonly JsonConverter<TV> _valueConverter;

        protected abstract IDictionary<TK, TV> InstantiateWorkingCollection();

        protected abstract TC InstantiateCollection(IDictionary<TK, TV> workingCollection);

        protected AbstractDictionaryConverter(JsonSerializerOptions options)
        {
            _keyConverter = TypeDescriptor.GetConverter(typeof(TK));
            if (!(_keyConverter.CanConvertFrom(typeof(string)) && _keyConverter.CanConvertTo(typeof(string))))
                throw new JsonException();

            _valueConverter = (JsonConverter<TV>)options.GetConverter(typeof(TV));
        }

        public override TC Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default!;
            }

            TC collection = default!;

            Read(ref reader, ref collection, options);

            return collection;
        }

        public void Read(ref Utf8JsonReader reader, ref TC obj, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            IDictionary<TK, TV> workingCollection = InstantiateWorkingCollection();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                TK key = (TK)_keyConverter.ConvertFromInvariantString(reader.GetString() ?? throw new JsonException());

                reader.Read();
                TV value = _valueConverter.Read(ref reader, typeof(TV), options);

                workingCollection.Add(key, value);
            }

            obj = InstantiateCollection(workingCollection);
        }

        public override void Write(Utf8JsonWriter writer, TC value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            foreach (KeyValuePair<TK, TV> kvp in value)
            {
                writer.WritePropertyName(_keyConverter.ConvertToInvariantString(kvp.Key) ?? throw new InvalidOperationException("Key cannot be converted to String"));

                _valueConverter.Write(writer, kvp.Value, options);
            }

            writer.WriteEndObject();
        }
=======
namespace Ark.Tools.SystemTextJson;

public abstract class AbstractDictionaryConverter<TC, TK, TV> : JsonConverter<TC>
    where TK : notnull
    where TC : IEnumerable<KeyValuePair<TK, TV>>
{
    private readonly TypeConverter _keyConverter;
    private readonly JsonConverter<TV> _valueConverter;

    protected abstract IDictionary<TK, TV> InstantiateWorkingCollection();

    protected abstract TC InstantiateCollection(IDictionary<TK, TV> workingCollection);

    protected AbstractDictionaryConverter(JsonSerializerOptions options)
    {
        _keyConverter = TypeDescriptor.GetConverter(typeof(TK));
        if (!(_keyConverter.CanConvertFrom(typeof(string)) && _keyConverter.CanConvertTo(typeof(string))))
            throw new JsonException();

        _valueConverter = (JsonConverter<TV>)options.GetConverter(typeof(TV));
    }

    public override TC Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default!;
        }

        TC collection = default!;

        Read(ref reader, ref collection, options);

        return collection;
    }

    public void Read(ref Utf8JsonReader reader, ref TC obj, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        IDictionary<TK, TV> workingCollection = InstantiateWorkingCollection();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            TK key = (TK)_keyConverter.ConvertFromInvariantString(reader.GetString() ?? throw new JsonException());

            reader.Read();
            TV value = _valueConverter.Read(ref reader, typeof(TV), options);

            workingCollection.Add(key, value);
        }

        obj = InstantiateCollection(workingCollection);
    }

    public override void Write(Utf8JsonWriter writer, TC value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (KeyValuePair<TK, TV> kvp in value)
        {
            writer.WritePropertyName(_keyConverter.ConvertToInvariantString(kvp.Key) ?? throw new InvalidOperationException("Key cannot be converted to String"));

            _valueConverter.Write(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
>>>>>>> After


#nullable disable

namespace Ark.Tools.SystemTextJson;

    public abstract class AbstractDictionaryConverter<TC, TK, TV> : JsonConverter<TC>
        where TK : notnull
        where TC : IEnumerable<KeyValuePair<TK, TV>>
    {
        private readonly TypeConverter _keyConverter;
        private readonly JsonConverter<TV> _valueConverter;

        protected abstract IDictionary<TK, TV> InstantiateWorkingCollection();

        protected abstract TC InstantiateCollection(IDictionary<TK, TV> workingCollection);

        protected AbstractDictionaryConverter(JsonSerializerOptions options)
        {
            _keyConverter = TypeDescriptor.GetConverter(typeof(TK));
            if (!(_keyConverter.CanConvertFrom(typeof(string)) && _keyConverter.CanConvertTo(typeof(string))))
                throw new JsonException();

            _valueConverter = (JsonConverter<TV>)options.GetConverter(typeof(TV));
        }

        public override TC Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default!;
            }

            TC collection = default!;

            Read(ref reader, ref collection, options);

            return collection;
        }

        public void Read(ref Utf8JsonReader reader, ref TC obj, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            IDictionary<TK, TV> workingCollection = InstantiateWorkingCollection();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                TK key = (TK)_keyConverter.ConvertFromInvariantString(reader.GetString() ?? throw new JsonException());

                reader.Read();
                TV value = _valueConverter.Read(ref reader, typeof(TV), options);

                workingCollection.Add(key, value);
            }

            obj = InstantiateCollection(workingCollection);
        }

        public override void Write(Utf8JsonWriter writer, TC value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            foreach (KeyValuePair<TK, TV> kvp in value)
            {
                writer.WritePropertyName(_keyConverter.ConvertToInvariantString(kvp.Key) ?? throw new InvalidOperationException("Key cannot be converted to String"));

                _valueConverter.Write(writer, kvp.Value, options);
            }

            writer.WriteEndObject();
        }
    }