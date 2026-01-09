using System;
using System.Text.Json;
using System.Text.Json.Serialization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.SystemTextJson(net10.0)', Before:
namespace Ark.Tools.SystemTextJson
{
    public class NullableStructSerializerFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            var t = Nullable.GetUnderlyingType(typeToConvert);
            return t != null;
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var structType = typeToConvert.GenericTypeArguments[0];

            return (JsonConverter?)Activator.CreateInstance(typeof(NullableStructSerializer<>).MakeGenericType(structType));
        }
=======
namespace Ark.Tools.SystemTextJson;

public class NullableStructSerializerFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var t = Nullable.GetUnderlyingType(typeToConvert);
        return t != null;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var structType = typeToConvert.GenericTypeArguments[0];

        return (JsonConverter?)Activator.CreateInstance(typeof(NullableStructSerializer<>).MakeGenericType(structType));
>>>>>>> After


namespace Ark.Tools.SystemTextJson;

public class NullableStructSerializerFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var t = Nullable.GetUnderlyingType(typeToConvert);
        return t != null;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var structType = typeToConvert.GenericTypeArguments[0];

        return (JsonConverter?)Activator.CreateInstance(typeof(NullableStructSerializer<>).MakeGenericType(structType));
    }
}