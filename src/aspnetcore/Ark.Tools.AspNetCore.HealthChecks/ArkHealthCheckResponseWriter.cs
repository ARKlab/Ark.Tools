// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ark.Tools.AspNetCore.HealthChecks;

/// <summary>Writes the shared JSON response for Ark health-check endpoints.</summary>
public static class ArkHealthCheckResponseWriter
{
    /// <summary>Writes a JSON health report response.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="healthReport">The health report to serialize.</param>
    public static async Task WriteResponseAsync(HttpContext context, HealthReport healthReport)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(healthReport);

        context.Response.ContentType = "application/json; charset=utf-8";

        var jsonWriter = new System.Text.Json.Utf8JsonWriter(context.Response.Body, new System.Text.Json.JsonWriterOptions { Indented = true });
        await using (jsonWriter.ConfigureAwait(false))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("status", healthReport.Status.ToString());
            jsonWriter.WriteStartObject("results");

            foreach (var entry in healthReport.Entries)
            {
                jsonWriter.WriteStartObject(entry.Key);
                jsonWriter.WriteString("status", entry.Value.Status.ToString());
                jsonWriter.WriteString("description", entry.Value.Description);
                // Intentionally omit exception details from the HTTP response.
                jsonWriter.WriteStartObject("data");

                foreach (var item in entry.Value.Data)
                {
                    jsonWriter.WritePropertyName(item.Key);
                    WriteValue(jsonWriter, item.Value);
                }

                jsonWriter.WriteEndObject();
                jsonWriter.WriteEndObject();

            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();

            await jsonWriter.FlushAsync(context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static void WriteValue(System.Text.Json.Utf8JsonWriter jsonWriter, object? value)
    {
        ArgumentNullException.ThrowIfNull(jsonWriter);

        switch (value)
        {
            case null:
                jsonWriter.WriteNullValue();
                break;
            case string stringValue:
                jsonWriter.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                jsonWriter.WriteBooleanValue(boolValue);
                break;
            case byte byteValue:
                jsonWriter.WriteNumberValue(byteValue);
                break;
            case sbyte sbyteValue:
                jsonWriter.WriteNumberValue(sbyteValue);
                break;
            case short shortValue:
                jsonWriter.WriteNumberValue(shortValue);
                break;
            case ushort ushortValue:
                jsonWriter.WriteNumberValue(ushortValue);
                break;
            case int intValue:
                jsonWriter.WriteNumberValue(intValue);
                break;
            case uint uintValue:
                jsonWriter.WriteNumberValue(uintValue);
                break;
            case long longValue:
                jsonWriter.WriteNumberValue(longValue);
                break;
            case ulong ulongValue:
                jsonWriter.WriteNumberValue(ulongValue);
                break;
            case float floatValue:
                jsonWriter.WriteNumberValue(floatValue);
                break;
            case double doubleValue:
                jsonWriter.WriteNumberValue(doubleValue);
                break;
            case decimal decimalValue:
                jsonWriter.WriteNumberValue(decimalValue);
                break;
            default:
                jsonWriter.WriteStringValue(value.ToString());
                break;
        }
    }
}
