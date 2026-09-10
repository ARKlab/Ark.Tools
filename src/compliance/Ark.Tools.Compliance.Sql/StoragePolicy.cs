// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance.Sql;

/// <summary>Describes the explicitly chosen protection for a persisted column.</summary>
public enum StoragePolicy
{
    /// <summary>Records an explicit decision to store the value without a generated mask.</summary>
    None,

    /// <summary>Applies SQL Server dynamic data masking to the column.</summary>
    Masked,

    /// <summary>Declares application-managed encryption; does not provision encryption or keys.</summary>
    ApplicationEncrypted,
}
