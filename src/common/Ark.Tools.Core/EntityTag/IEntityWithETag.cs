// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core.EntityTag;

public interface IEntityWithETag
{
#pragma warning disable IDE1006 // Public API name is fixed for compatibility.
    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "By design")]
    string? _ETag { get; set; }
#pragma warning restore IDE1006
}