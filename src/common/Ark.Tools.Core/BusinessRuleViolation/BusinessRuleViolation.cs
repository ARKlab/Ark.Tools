// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core.BusinessRuleViolation;

#pragma warning disable MA0049 // Type name should not match containing namespace
#pragma warning disable CA1724 // Type names should not match namespaces
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class BusinessRuleViolation
#pragma warning restore CA1724 // Type names should not match namespaces
#pragma warning restore MA0049 // Type name should not match containing namespace
{
    public BusinessRuleViolation(string title)
    {
        Title = title;
    }

    public int Status { get; set; } = 400;
    public string Title { get; set; }
    public string? Detail { get; set; }
}