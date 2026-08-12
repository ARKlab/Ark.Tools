// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Core.BusinessRuleViolation;

/// <summary>Marks a business-rule violation property for client-visible error extensions.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ProblemDetailsExtensionAttribute : Attribute
{
}
