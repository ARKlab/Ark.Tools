// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Core;
using Ark.Tools.MediatorFramework.Generated;
using Ark.Tools.Solid;
using Ark.Tools.SystemTextJson;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using System.Data;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.MediatorFramework.NativeAotApp;

/// <summary>
/// Exercises Vogen value objects through the Ark.Tools features that support Native AOT. CI runs it after a
/// Native AOT publish; it exits non-zero when a check fails. OpenAPI is not exercised: document generation pulls
/// ASP.NET Core MVC's model metadata, which is not Native AOT compatible.
/// </summary>
internal static class Program
{
    private static readonly Guid _id = Guid.Parse("8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f");

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
        builder.Services.AddSingleton<IQueryHandler<GetBookQuery, Book>, GetBookQueryHandler>();
        builder.Services.AddSingleton<IQueryProcessor, BookQueryProcessor>();

        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        app.UseRouting();
        app.UseAuthorization();
        app.MapArkEndpoints<AppEndpointContext>();
        var ctk = app.Lifetime.ApplicationStopping;
        await app.StartAsync(ctk).ConfigureAwait(false);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        using var ok = await http.GetAsync(new Uri("/books/" + _id.ToString("D"), UriKind.Relative), ctk).ConfigureAwait(false);
        var body = await ok.Content.ReadAsStringAsync(ctk).ConfigureAwait(false);
        check("endpoint-binds-value-object-route", () => ok.StatusCode == HttpStatusCode.OK && body.Contains(_id.ToString("D"), StringComparison.Ordinal));
        using var bad = await http.GetAsync(new Uri("/books/not-a-guid", UriKind.Relative), ctk).ConfigureAwait(false);
        check("endpoint-rejects-invalid-value-object", () => bad.StatusCode == HttpStatusCode.BadRequest);

        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
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
