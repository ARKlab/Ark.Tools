// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Authorization;

/// <summary>Indicates that a handler policy denied the current principal.</summary>
public sealed class PolicyAuthorizationException : UnauthorizedAccessException
{
    /// <summary>Initializes a new instance of the <see cref="PolicyAuthorizationException"/> class.</summary>
    public PolicyAuthorizationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PolicyAuthorizationException"/> class.</summary>
    /// <param name="message">The policy failure details.</param>
    public PolicyAuthorizationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PolicyAuthorizationException"/> class.</summary>
    /// <param name="message">The policy failure details.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PolicyAuthorizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
