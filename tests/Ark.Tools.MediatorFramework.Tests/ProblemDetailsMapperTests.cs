// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Authorization;
using Ark.Tools.Core;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies shared exception-to-ProblemDetails mappings used by mediator hosts.</summary>
[TestClass]
public sealed class ProblemDetailsMapperTests
{
    [TestMethod]
    public void MapsKnownExceptionsToExpectedStatusCodes()
    {
        ExceptionProblemDetailsMapper.Map(new PolicyAuthorizationException()).Status.Should().Be(StatusCodes.Status403Forbidden);
        ExceptionProblemDetailsMapper.Map(new EntityNotFoundException()).Status.Should().Be(StatusCodes.Status404NotFound);
        ExceptionProblemDetailsMapper.Map(new ValidationException([new ValidationFailure("Name", "Required")])).Status.Should().Be(StatusCodes.Status400BadRequest);
        ExceptionProblemDetailsMapper.Map(new OptimisticConcurrencyException()).Status.Should().Be(StatusCodes.Status409Conflict);
        ExceptionProblemDetailsMapper.Map(new InvalidOperationException()).Status.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
