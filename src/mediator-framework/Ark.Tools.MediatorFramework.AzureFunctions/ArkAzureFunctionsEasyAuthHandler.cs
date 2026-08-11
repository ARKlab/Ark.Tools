// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.AzureFunctions;

internal sealed partial class ArkAzureFunctionsEasyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string _platformSwitch = "WEBSITE_AUTH_ENABLED";
    private const string _principalHeader = "X-MS-CLIENT-PRINCIPAL";
    private readonly ILogger _logger = logger.CreateLogger<ArkAzureFunctionsEasyAuthHandler>();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(_platformSwitch), "True", StringComparison.OrdinalIgnoreCase))
            return await Task.FromResult(AuthenticateResult.Fail("Easy Auth is not enabled for this deployment.")).ConfigureAwait(false);

        if (!Request.Headers.TryGetValue(_principalHeader, out var values))
            return await Task.FromResult(AuthenticateResult.NoResult()).ConfigureAwait(false);

        var encoded = values.ToString();
        var limit = Context.RequestServices.GetService(typeof(IOptions<ArkAzureFunctionsAuthenticationOptions>))
            is IOptions<ArkAzureFunctionsAuthenticationOptions> configured
            ? configured.Value.EasyAuthHeaderLimit
            : 16 * 1024;
        if (limit <= 0 || encoded.Length > limit)
        {
            HeaderTooLarge(_logger);
            return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("claims", out var claims)
                || claims.ValueKind != JsonValueKind.Array
                || claims.GetArrayLength() > 128)
                return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);

            var identity = new ClaimsIdentity("EasyAuth");
            foreach (var claim in claims.EnumerateArray())
            {
                if (claim.ValueKind != JsonValueKind.Object
                    || !claim.TryGetProperty("typ", out var type)
                    || !claim.TryGetProperty("val", out var value)
                    || type.ValueKind != JsonValueKind.String
                    || value.ValueKind != JsonValueKind.String)
                    return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);

                var claimType = type.GetString();
                var claimValue = value.GetString();
                if (string.IsNullOrWhiteSpace(claimType)
                    || claimType.Length > 256
                    || claimValue is null
                    || claimValue.Length > 2048)
                    return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);
                identity.AddClaim(new Claim(claimType, claimValue));
            }

            if (!identity.Claims.Any())
                return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);
            return await Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name))).ConfigureAwait(false);
        }
        catch (FormatException)
        {
            InvalidBase64(_logger);
            return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            InvalidJson(_logger);
            return await Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid.")).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Easy Auth principal header was rejected because it exceeded the configured size limit.")]
    private static partial void HeaderTooLarge(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Easy Auth principal header was rejected because it was not valid base64.")]
    private static partial void InvalidBase64(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Easy Auth principal header was rejected because it was not valid JSON.")]
    private static partial void InvalidJson(ILogger logger);
}
