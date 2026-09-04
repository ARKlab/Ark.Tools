// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Net.Mail;

namespace Ark.Tools.Compliance;

[SensitiveValueObject<string>(ArkRedaction.Mask)]
public readonly partial struct EmailAddress
{
    private static ValidationResult _validate(string value)
    {
        return MailAddress.TryCreate(value, out _) && value.Length <= 320
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The email address is invalid.");
    }

    private static string _normalize(string value) => value.Trim().ToLowerInvariant();
}

[SensitiveValueObject<string>(ArkRedaction.Mask)]
public readonly partial struct PhoneNumber
{
    private static ValidationResult _validate(string value)
    {
        var digits = 0;
        foreach (var character in value)
        {
            if (character == '+' && digits == 0)
                continue;
            if (character is < '0' or > '9')
                return ValidationResult.Invalid("The phone number is invalid.");
            digits++;
        }

        return digits is >= 7 and <= 15
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The phone number is invalid.");
    }

    private static string _normalize(string value)
    {
        return value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);
    }
}

[SensitiveValueObject<string>(ArkRedaction.Mask)]
public readonly partial struct PersonName
{
    private static ValidationResult _validate(string value)
    {
        return value.Length is >= 1 and <= 200
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The person name is invalid.");
    }

    private static string _normalize(string value) => value.Trim();
}

[SensitiveValueObject<string>(ArkRedaction.Erase)]
public readonly partial struct PostalAddressLine
{
    private static ValidationResult _validate(string value)
    {
        return value.Length is >= 1 and <= 500
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The postal address is invalid.");
    }

    private static string _normalize(string value) => value.Trim();
}

[SensitiveValueObject<string>(ArkRedaction.Erase)]
public readonly partial struct NationalIdentifier
{
    private static ValidationResult _validate(string value)
    {
        return value.Length is >= 1 and <= 100
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The national identifier is invalid.");
    }

    private static string _normalize(string value) => value.Trim();
}

[SensitiveValueObject<string>(ArkRedaction.Erase)]
public readonly partial struct ApiKey
{
    private static ValidationResult _validate(string value)
    {
        return value.Length is >= 1 and <= 4096
            ? ValidationResult.Ok
            : ValidationResult.Invalid("The API key is invalid.");
    }

    private static string _normalize(string value) => value.Trim();
}
