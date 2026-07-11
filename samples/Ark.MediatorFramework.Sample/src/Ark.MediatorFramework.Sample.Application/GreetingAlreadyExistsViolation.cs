// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Business rule violation raised when a greeting name has already been used.</summary>
public sealed class GreetingAlreadyExistsViolation : BusinessRuleViolation
{
    /// <summary>Initializes a new instance of the <see cref="GreetingAlreadyExistsViolation"/> class.</summary>
    /// <param name="name">The duplicate greeting name.</param>
    public GreetingAlreadyExistsViolation(string name)
        : base("Greeting already exists")
    {
        Name = name;
        Detail = $"A greeting for '{name}' already exists.";
    }

    /// <summary>Gets the duplicate greeting name.</summary>
    public string Name { get; }
}
