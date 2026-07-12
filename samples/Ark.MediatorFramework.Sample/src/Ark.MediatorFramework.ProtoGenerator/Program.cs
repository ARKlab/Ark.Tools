// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;
using System.Text;

var assembly = Assembly.LoadFrom(args[0]);
var output = args[1];
var contracts = assembly.GetTypes()
    .Where(static type => type.GetCustomAttributesData().Any(static attribute => attribute.AttributeType.Name == "ProtoContractAttribute"))
    .ToDictionary(static type => type, static type => type.Name);
var grpcMethods = assembly.GetTypes()
    .Select(type => new
    {
        Type = type,
        Grpc = type.GetCustomAttributesData().FirstOrDefault(static attribute => attribute.AttributeType.Name == "GrpcMethodAttribute"),
        Group = type.GetCustomAttributesData().FirstOrDefault(static attribute => attribute.AttributeType.Name == "ServiceGroupAttribute"),
    })
    .Where(static item => item.Grpc is not null)
    .GroupBy(static item => item.Group?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "Ark")
    .ToArray();

var schema = new StringBuilder();
schema.AppendLine("syntax = \"proto3\";");
schema.AppendLine();
schema.AppendLine("option csharp_namespace = \"Ark.MediatorFramework.Sample.GrpcClient\";");
schema.AppendLine();
schema.AppendLine("message LocalDate { int32 year = 1; int32 month = 2; int32 day = 3; }");
schema.AppendLine("message LocalDateTime { int32 year = 1; int32 month = 2; int32 day = 3; int64 nanosecond_of_day = 4; }");
schema.AppendLine("message OffsetDateTime { int32 year = 1; int32 month = 2; int32 day = 3; int64 nanosecond_of_day = 4; int32 offset_seconds = 5; }");
schema.AppendLine("message Period { string value = 1; }");
schema.AppendLine("message ArkBusinessRuleViolation { string type = 1; string title = 2; int32 status = 3; string payload_json = 4; }");
schema.AppendLine();

foreach (var contract in contracts.OrderBy(static pair => pair.Value, StringComparer.Ordinal))
{
    var members = contract.Key.GetProperties()
        .Select(property => new
        {
            Property = property,
            ProtoMember = property.GetCustomAttributesData()
                .FirstOrDefault(static attribute => attribute.AttributeType.Name == "ProtoMemberAttribute"),
        })
        .Where(static item => item.ProtoMember is not null)
        .Select(item => new
        {
            item.Property,
            Number = Convert.ToInt32(item.ProtoMember!.ConstructorArguments[0].Value),
        })
        .OrderBy(static item => item.Number);

    schema.Append("message ").Append(contract.Value).AppendLine(" {");
    foreach (var member in members)
    {
        var repeated = TryGetElementType(member.Property.PropertyType, out var elementType);
        var type = ProtoTypeName(repeated ? elementType! : member.Property.PropertyType, contracts);
        schema.Append("  ");
        if (repeated)
            schema.Append("repeated ");
        schema.Append(type).Append(' ').Append(ToSnakeCase(member.Property.Name)).Append(" = ")
            .Append(member.Number).AppendLine(";");
    }

    schema.AppendLine("}");
    schema.AppendLine();
}

foreach (var group in grpcMethods.OrderBy(static group => group.Key, StringComparer.Ordinal))
{
    schema.Append("service ").Append(group.Key).AppendLine(" {");
    foreach (var method in group.OrderBy(static item => item.Type.Name, StringComparer.Ordinal))
    {
        var response = method.Type.GetInterfaces()
            .FirstOrDefault(static item => item.IsGenericType && item.GetGenericTypeDefinition().Name is "IRequest`1" or "IQuery`1")
            ?.GetGenericArguments()[0];
        if (response is null)
            continue;

        var name = method.Grpc!.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? method.Type.Name;
        schema.Append("  rpc ").Append(name).Append('(').Append(method.Type.Name).Append(") returns (")
            .Append(response.Name).AppendLine(");");
    }

    schema.AppendLine("}");
    schema.AppendLine();
}

schema.AppendLine("message UploadDocumentMetadata { string name = 1; string content_type = 2; }");
schema.AppendLine("message UploadDocumentChunk {");
schema.AppendLine("  oneof content { UploadDocumentMetadata metadata = 1; bytes data = 2; }");
schema.AppendLine("}");
schema.AppendLine("service Documents {");
schema.AppendLine("  rpc Upload(stream UploadDocumentChunk) returns (UploadResponse);");
schema.AppendLine("}");
schema.AppendLine();

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, schema.ToString(), new UTF8Encoding(false));

static bool TryGetElementType(Type type, out Type? elementType)
{
    elementType = type.IsArray
        ? type.GetElementType()
        : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? type.GetGenericArguments()[0]
            : null;
    return elementType is not null;
}

static string ProtoTypeName(Type type, IReadOnlyDictionary<Type, string> contracts)
{
    var nullable = Nullable.GetUnderlyingType(type);
    if (nullable is not null)
        type = nullable;

    if (contracts.TryGetValue(type, out var contractName))
        return contractName;

    if (type == typeof(string))
        return "string";
    if (type == typeof(Guid))
        return "bytes";
    if (type.FullName == "NodaTime.LocalDate")
        return "LocalDate";
    if (type.FullName == "NodaTime.LocalDateTime")
        return "LocalDateTime";
    if (type.FullName == "NodaTime.OffsetDateTime")
        return "OffsetDateTime";
    if (type.FullName == "NodaTime.Period")
        return "Period";
    if (type == typeof(bool))
        return "bool";
    if (type == typeof(long) || type == typeof(ulong))
        return type == typeof(long) ? "int64" : "uint64";
    if (type == typeof(int) || type == typeof(short) || type == typeof(byte))
        return "int32";
    if (type == typeof(uint) || type == typeof(ushort))
        return "uint32";
    if (type == typeof(float))
        return "float";
    if (type == typeof(double))
        return "double";
    if (type.IsEnum)
        return type.Name;

    return "bytes";
}

static string ToSnakeCase(string value)
{
    var builder = new StringBuilder(value.Length + 4);
    foreach (var character in value)
    {
        if (char.IsUpper(character) && builder.Length > 0)
            builder.Append('_');
        builder.Append(char.ToLowerInvariant(character));
    }

    return builder.ToString();
}
