// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Tests.Init;
using Ark.Tools.SystemTextJson;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Reqnroll;

using System.Text.Json;

namespace Ark.Reference.Core.Tests.Features;

[Binding]
public sealed class SerializationSteps
{
    [Then("the HTTP JSON options use the Ark defaults and respect nullable annotations and required constructor parameters")]
    public void ThenTheHttpJsonOptionsUseTheArkDefaultsAndRespectNullableAnnotationsAndRequiredConstructorParameters()
    {
        var services = TestHost.Server.Services;
        _shouldBeStrict(services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions);
        _shouldBeStrict(services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions);
    }

    // A context used as a resolver follows the consuming options, so the Ark defaults and flags must be set on them.
    private static void _shouldBeStrict(JsonSerializerOptions options)
    {
        options.Converters.OfType<ArkJsonStringEnumConverter>().Should().ContainSingle();
        options.RespectNullableAnnotations.Should().BeTrue();
        options.RespectRequiredConstructorParameters.Should().BeTrue();
    }
}
