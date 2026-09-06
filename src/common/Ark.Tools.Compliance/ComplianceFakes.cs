// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Produces deterministic values drawn only from reserved ranges, so a fixture, a
/// schema example, or a log sample can never contain a real person's data.
/// </summary>
/// <remarks>
/// Domains are the RFC 2606 reserved ones, phone numbers use the reserved fictional
/// ranges, and identifiers are documented invalid-checksum values. The same values back
/// the OpenAPI schema examples and the test-data fakes, so the reserved set is maintained
/// once.
/// </remarks>
public static class ComplianceFakes
{
    private static readonly string[] _emails =
    [
        "jane.doe@example.com",
        "john.doe@example.org",
        "test.user@example.net",
    ];

    // Reserved fictional ranges: +1 555 0100-0199 (NANP) and +44 7700 900000-900999 (Ofcom).
    private static readonly string[] _phones =
    [
        "+15550100",
        "+447700900123",
        "+15550199",
    ];

    private static readonly string[] _names =
    [
        "Jane Doe",
        "John Doe",
        "Test User",
    ];

    private static readonly string[] _addresses =
    [
        "1 Example Street, Example City",
        "2 Example Avenue, Example Town",
        "3 Example Road, Example Village",
    ];

    // Documented invalid-checksum identifiers: never allocatable to a real person.
    private static readonly string[] _identifiers =
    [
        "XXXXXX00X00X000X",
        "000-00-0000",
        "XX00000000",
    ];

    private static readonly string[] _apiKeys =
    [
        "example-api-key-0000",
        "example-api-key-0001",
        "example-api-key-0002",
    ];

    /// <summary>Gets a reserved email address.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string Email(int seed = 0) => _pick(_emails, seed);

    /// <summary>Gets a reserved, never-allocatable phone number.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string PhoneNumber(int seed = 0) => _pick(_phones, seed);

    /// <summary>Gets a fictional person name.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string PersonName(int seed = 0) => _pick(_names, seed);

    /// <summary>Gets a non-routable postal address line.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string PostalAddressLine(int seed = 0) => _pick(_addresses, seed);

    /// <summary>Gets an invalid-checksum national identifier.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string NationalIdentifier(int seed = 0) => _pick(_identifiers, seed);

    /// <summary>Gets a non-functional API key.</summary>
    /// <param name="seed">Selects a stable value; the same seed always yields the same value.</param>
    public static string ApiKey(int seed = 0) => _pick(_apiKeys, seed);

    private static string _pick(string[] values, int seed)
    {
        // ponytail: modulo over a fixed reserved table keeps the output deterministic and
        // provably reserved. If a caller ever needs unique values per seed, add a suffix
        // built from the seed rather than switching to a random generator.
        return values[(int)((uint)seed % (uint)values.Length)];
    }
}
