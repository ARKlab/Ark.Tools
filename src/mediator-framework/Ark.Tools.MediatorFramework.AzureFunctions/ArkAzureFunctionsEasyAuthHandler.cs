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
    private const string PlatformSwitch = "WEBSITE_AUTH_ENABLED";
    private const string PrincipalHeader = "X-MS-CLIENT-PRINCIPAL";
    private readonly ILogger _logger = logger.CreateLogger<ArkAzureFunctionsEasyAuthHandler>();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(PlatformSwitch), "True", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Easy Auth is not enabled for this deployment."));

        if (!Request.Headers.TryGetValue(PrincipalHeader, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var encoded = values.ToString();
        var limit = Context.RequestServices.GetService(typeof(IOptions<ArkAzureFunctionsAuthenticationOptions>))
            is IOptions<ArkAzureFunctionsAuthenticationOptions> configured
            ? configured.Value.EasyAuthHeaderLimit
            : 16 * 1024;
        if (limit <= 0 || encoded.Length > limit)
        {
            HeaderTooLarge(_logger);
            return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            if (!document.RootElement.TryGetProperty("claims", out var claims)
                || claims.ValueKind != JsonValueKind.Array
                || claims.GetArrayLength() > 128)
                return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));

            var identity = new ClaimsIdentity("EasyAuth");
            foreach (var claim in claims.EnumerateArray())
            {
                if (!claim.TryGetProperty("typ", out var type)
                    || !claim.TryGetProperty("val", out var value)
                    || type.ValueKind != JsonValueKind.String
                    || value.ValueKind != JsonValueKind.String)
                    return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));

                var claimType = type.GetString();
                var claimValue = value.GetString();
                if (string.IsNullOrWhiteSpace(claimType)
                    || claimType.Length > 256
                    || claimValue is null
                    || claimValue.Length > 2048)
                    return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));
                identity.AddClaim(new Claim(claimType, claimValue));
            }

            if (!identity.Claims.Any())
                return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
        catch (FormatException)
        {
            InvalidBase64(_logger);
            return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));
        }
        catch (JsonException)
        {
            InvalidJson(_logger);
            return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal is invalid."));
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
