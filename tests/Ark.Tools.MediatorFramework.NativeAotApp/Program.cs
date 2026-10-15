// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Core;
using Ark.Tools.MediatorFramework.Generated;
using Ark.Tools.MediatorFramework.Mcp;
using Ark.Tools.Solid;
using Ark.Tools.SystemTextJson;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using MessagePack;
using MessagePack.Resolvers;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.MediatorFramework.NativeAotApp;

/// <summary>
/// Exercises Vogen value objects through the Ark.Tools features that support Native AOT, and generated MCP tools
/// through an in-process MCP client. CI runs it after a Native AOT publish; it exits non-zero when a check fails.
/// OpenAPI is not exercised: document generation pulls ASP.NET Core MVC's model metadata, which is not Native AOT
/// compatible.
/// </summary>
internal static class Program
{
    private static readonly Guid _id = Guid.Parse("8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f");
    private static readonly IFormatterResolver _messagePackResolver = CompositeResolver.Create(GeneratedMessagePackResolver.Instance, BuiltinResolver.Instance);

    private static async Task Main(string[] args)
    {
        var failures = new List<string>();
        void Check(string name, Func<bool> check)
        {
            try
            {
                if (!check())
                    failures.Add(name);
            }
#pragma warning disable CA1031, ERP022 // Report every failing check, whatever it throws.
            catch (Exception ex)
            {
                failures.Add(name + ": " + ex);
            }
#pragma warning restore CA1031, ERP022
        }

        _checkJson(Check);
        _checkDataTable(Check);
        await _checkHttpAsync(args, Check).ConfigureAwait(false);

        if (failures.Count > 0)
            throw new InvalidOperationException("Native AOT checks failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static void _checkJson(Action<string, Func<bool>> check)
    {
        var options = _jsonOptions(new JsonSerializerOptions { RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true });
        var book = new Book { Id = BookId.From(_id), Code = BookCode.From("B-1"), Related = [BookId.From(_id)] };

        check("json", () =>
        {
            var typeInfo = (JsonTypeInfo<Book>)options.GetTypeInfo(typeof(Book));
            var json = JsonSerializer.Serialize(book, typeInfo);
            var read = JsonSerializer.Deserialize(json, typeInfo);
            return string.Equals(json, """{"id":"8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f","parentId":null,"code":"B-1","related":["8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f"]}""", StringComparison.Ordinal)
                && read is not null && read.Id == book.Id && read.Code == book.Code && read.Related.SequenceEqual(book.Related);
        });
    }

    private static void _checkDataTable(Action<string, Func<bool>> check)
    {
        var rows = new[] { new BookRow { Id = BookId.From(_id), ParentId = null, Code = BookCode.From("B-1") } };

        check("datatable-intercepted", () =>
        {
            using var table = rows.ToDataTableArk();
            return table.Columns["Id"]!.DataType == typeof(Guid) && table.Columns["Code"]!.DataType == typeof(string)
                && (Guid)table.Rows[0]["Id"] == _id && (string)table.Rows[0]["Code"] == "B-1" && table.Rows[0]["ParentId"] is DBNull;
        });
        check("datatable-fallback-plain", static () =>
        {
            using var table = _fallback(new[] { new PlainRow { Id = _id, ParentId = null } });
            return (Guid)table.Rows[0]["Id"] == _id && table.Rows[0]["ParentId"] is DBNull;
        });
        check("datatable-fallback-value-object-fails-loudly", () =>
        {
            try
            {
                using var table = _fallback(rows);
                return false;
            }
            catch (TypeInitializationException ex) when (ex.InnerException is NotSupportedException)
            {
                return true;
            }
        });
    }

    private static async Task _checkHttpAsync(string[] args, Action<string, Func<bool>> check)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(static options => _jsonOptions(options.SerializerOptions));
        builder.Services.AddAuthorization(static options =>
            options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(static _ => true).Build());
        builder.Services.AddScoped<IQueryHandler<GetBookQuery, Book>, GetBookQueryHandler>();
        builder.Services.AddScoped<IRequestHandler<UploadBookRequest, Book>, UploadBookRequestHandler>();
        builder.Services.AddScoped<IQueryHandler<DownloadBookQuery, IArkAttachment>, DownloadBookQueryHandler>();
        builder.Services.AddScoped<IRequestHandler<RenameBookRequest, RenamedBook>, RenameBookRequestHandler>();
        builder.Services.AddSingleton<IFormatterResolver>(_messagePackResolver);
        builder.Services.AddArkSolidServiceProviderProcessors();
        builder.Services.AddMcpServer().WithHttpTransport().WithArkMcpTools<AppMcpContext>();

        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        app.UseRouting();
        app.UseAuthorization();
        app.MapArkEndpoints<AppEndpointContext>();
        app.MapMcp("/mcp");
        var ctk = app.Lifetime.ApplicationStopping;
        await app.StartAsync(ctk).ConfigureAwait(false);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        using var ok = await http.GetAsync(new Uri("/books/" + _id.ToString("D"), UriKind.Relative), ctk).ConfigureAwait(false);
        var body = await ok.Content.ReadAsStringAsync(ctk).ConfigureAwait(false);
        check("endpoint-binds-value-object-route", () => ok.StatusCode == HttpStatusCode.OK && body.Contains(_id.ToString("D"), StringComparison.Ordinal));
        using var bad = await http.GetAsync(new Uri("/books/not-a-guid", UriKind.Relative), ctk).ConfigureAwait(false);
        check("endpoint-rejects-invalid-value-object", () => bad.StatusCode == HttpStatusCode.BadRequest);

        await _checkMessagePackEndpointAsync(http, check, ctk).ConfigureAwait(false);
        await _checkMcpAsync(http, new Uri(address + "/mcp"), check, ctk).ConfigureAwait(false);

        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // A MessagePack-negotiated endpoint must still serve JSON through the host's source-generated metadata.
    private static async Task _checkMessagePackEndpointAsync(HttpClient http, Action<string, Func<bool>> check, CancellationToken ctk)
    {
        var uri = new Uri("/books/rename", UriKind.Relative);
        var request = new RenameBookRequest { Code = "B-2" };

        using var json = await http.PostAsJsonAsync(uri, request, AppJsonContext.Default.RenameBookRequest, ctk).ConfigureAwait(false);
        var jsonBody = await json.Content.ReadAsStringAsync(ctk).ConfigureAwait(false);
        check("messagepack-endpoint-json", () => json.StatusCode == HttpStatusCode.OK
            && string.Equals(jsonBody, """{"code":"B-2-renamed"}""", StringComparison.Ordinal));

        var options = MessagePackSerializerOptions.Standard.WithResolver(_messagePackResolver);
        using var content = new ByteArrayContent(MessagePackSerializer.Serialize(request, options, ctk));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
        using var message = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-msgpack"));
        using var packed = await http.SendAsync(message, ctk).ConfigureAwait(false);
        var packedBody = await packed.Content.ReadAsByteArrayAsync(ctk).ConfigureAwait(false);
        check("messagepack-endpoint-msgpack", () => packed.StatusCode == HttpStatusCode.OK
            && MessagePackSerializer.Deserialize<RenamedBook>(packedBody, options, ctk).Code == "B-2-renamed");
    }

    // The tools bind the app's source-generated contracts; the attachment and protocol types must come from McpToolSerializerOptions.
    private static async Task _checkMcpAsync(HttpClient http, Uri endpoint, Action<string, Func<bool>> check, CancellationToken ctk)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp },
            http,
            NullLoggerFactory.Instance,
            ownsHttpClient: false);
        await using var __transport = transport.ConfigureAwait(false);
        var client = await McpClient.CreateAsync(transport, cancellationToken: ctk).ConfigureAwait(false);
        await using var __client = client.ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: ctk).ConfigureAwait(false);
        check("mcp-list-tools", () => tools.Select(static t => t.Name).Order(StringComparer.Ordinal).SequenceEqual(["books.download", "books.get", "books.upload"], StringComparer.Ordinal));

        var get = await client.CallToolAsync("books.get", new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = _id.ToString("D") }, cancellationToken: ctk).ConfigureAwait(false);
        check("mcp-query", () => get.IsError != true && get.StructuredContent?.GetProperty("id").GetString() == _id.ToString("D"));

        using var attachment = JsonDocument.Parse("""{"name":"a.txt","mimeType":"text/plain","blob":"aGVsbG8="}""");
        var upload = await client.CallToolAsync("books.upload", new Dictionary<string, object?>(StringComparer.Ordinal) { ["attachment"] = attachment.RootElement }, cancellationToken: ctk).ConfigureAwait(false);
        check("mcp-attachment-upload", () => upload.IsError != true && upload.StructuredContent?.GetProperty("code").GetString() == "a.txt:hello");

        var download = await client.CallToolAsync("books.download", new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "b.txt" }, cancellationToken: ctk).ConfigureAwait(false);
        check("mcp-attachment-download", () => download.IsError != true
            && download.Content.OfType<EmbeddedResourceBlock>().SingleOrDefault()?.Resource is BlobResourceContents { MimeType: "text/plain" } blob
            && blob.DecodedData.Span.SequenceEqual("payload"u8));
    }

    // Native AOT cannot use ConfigureArkDefaults: add the source-generated context and the value object converter.
    private static JsonSerializerOptions _jsonOptions(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
        options.Converters.Add(new ValueObjectJsonConverterFactory());
        return options;
    }

    // An open generic call site cannot be intercepted, so ToDataTableArk() uses its reflection fallback.
    private static DataTable _fallback<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>(IEnumerable<T> items)
    {
        return items.ToDataTableArk();
    }
}
