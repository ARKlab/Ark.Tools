// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>Controls exception details included in HTTP ProblemDetails responses.</summary>
public sealed class ArkProblemDetailsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether exception details are included outside development.
    /// Development environments always include details.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
