// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>The authenticated user identifier recorded with reviews, reading activity and audits.</summary>
/// <remarks>
/// A compliance sensitive value object rather than a Vogen one: the identifier is pseudonymous personal data, so
/// every display surface (<c>ToString</c>, interpolation, logs) is redacted and cleartext needs an explicit
/// <c>Reveal</c>.
/// </remarks>
[Pseudonymous]
[SensitiveValueObject<string>(ArkRedaction.Hmac)]
public readonly partial struct UserId
{
    /// <summary>Gets the identifier recorded when no user is authenticated.</summary>
    public static UserId Anonymous { get; } = From("anonymous");
}
