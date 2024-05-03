// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core.EntityTag
{
    public interface IEntityWithETag
    {
        string? _ETag { get; set; }
    }
}
