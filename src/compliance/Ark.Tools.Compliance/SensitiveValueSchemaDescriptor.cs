// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance;

/// <summary>
/// Describes how a sensitive value object is documented in an OpenAPI document: as its
/// primitive string schema, carrying the Ark classification as a vendor extension.
/// </summary>
/// <param name="Type">The sensitive value object.</param>
/// <param name="NullableType">The nullable form of <paramref name="Type"/>.</param>
/// <param name="Classification">The data classification recorded in the document.</param>
/// <param name="Example">A reserved example value; see <see cref="ComplianceFakes"/>.</param>
/// <param name="Format">The optional OpenAPI string format.</param>
/// <remarks>
/// Descriptors are plain data with no OpenAPI library dependency, so each document generator
/// builds the schema with the <c>Microsoft.OpenApi</c> version it ships with.
/// </remarks>
public sealed record SensitiveValueSchemaDescriptor(
    Type Type,
    Type NullableType,
    DataClassification Classification,
    string Example,
    string? Format)
{
    /// <summary>The vendor extension carrying the Ark data classification.</summary>
    public const string ClassificationExtension = "x-ark-classification";

    /// <summary>The value of <see cref="ClassificationExtension"/> for this descriptor.</summary>
    public string ClassificationValue => $"{Classification.TaxonomyName}:{Classification.Value}";

    /// <summary>
    /// Creates a descriptor for a sensitive value object.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="classification">The data classification recorded in the document.</param>
    /// <param name="example">A reserved example value; see <see cref="ComplianceFakes"/>.</param>
    /// <param name="format">The optional OpenAPI string format.</param>
    /// <returns>The descriptor.</returns>
    public static SensitiveValueSchemaDescriptor For<T>(DataClassification classification, string example, string? format = null)
        where T : struct, ISensitiveValue<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(example);
        return new SensitiveValueSchemaDescriptor(typeof(T), typeof(T?), classification, example, format);
    }

    /// <summary>
    /// The descriptors of the sensitive value objects shipped with <c>Ark.Tools.Compliance</c>.
    /// </summary>
    public static IReadOnlyList<SensitiveValueSchemaDescriptor> ArkTypes { get; } =
    [
        For<EmailAddress>(ArkDataClassifications.PersonalData, ComplianceFakes.Email(), "email"),
        For<PhoneNumber>(ArkDataClassifications.PersonalData, ComplianceFakes.PhoneNumber()),
        For<PersonName>(ArkDataClassifications.PersonalData, ComplianceFakes.PersonName()),
        For<PostalAddressLine>(ArkDataClassifications.PersonalData, ComplianceFakes.PostalAddressLine()),
        For<NationalIdentifier>(ArkDataClassifications.SensitivePersonalData, ComplianceFakes.NationalIdentifier()),
        For<ApiKey>(ArkDataClassifications.InfrastructureSecret, ComplianceFakes.ApiKey(), "password"),
    ];
}
