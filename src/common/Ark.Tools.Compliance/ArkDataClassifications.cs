// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance;

/// <summary>
/// Provides Ark's data classification taxonomy.
/// </summary>
public static class ArkDataClassifications
{
    /// <summary>
    /// The name of the Ark classification taxonomy.
    /// </summary>
    public const string TaxonomyName = "Ark";

    /// <summary>
    /// Directly identifies a natural person.
    /// </summary>
    public static DataClassification PersonalData => new(TaxonomyName, nameof(PersonalData));

    /// <summary>
    /// Represents special categories of personal data.
    /// </summary>
    public static DataClassification SensitivePersonalData => new(TaxonomyName, nameof(SensitivePersonalData));

    /// <summary>
    /// Represents credentials, keys, tokens, and connection strings.
    /// </summary>
    public static DataClassification Secret => new(TaxonomyName, nameof(Secret));

    /// <summary>
    /// Represents data that is re-identifiable only with additional data.
    /// </summary>
    public static DataClassification Pseudonymous => new(TaxonomyName, nameof(Pseudonymous));
}
