// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

#if NETSTANDARD2_0
using System;
using System.Linq;
using System.Text;
#endif

namespace Ark.Tools.Compliance.Internal;

internal enum PersonalDataKind
{
    Email,
    Phone,
    NationalIdentifier,
    Iban,
    PostalAddress,
}

internal static class PersonalDataPatterns
{
    internal const string _email = @"(?<![\w.+-])[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+(?![\w-])";
    internal const string _phone = @"(?<![\w\d])(?:\+[1-9](?:[ ()-]*[0-9]){7,14}|\(?[2-9][0-9]{2}\)?[ .-][2-9][0-9]{2}[ .-][0-9]{4})(?![0-9])";
    internal const string _nationalIdentifier = @"(?<![A-Za-z0-9])(?:[0-9]{3}-[0-9]{2}-[0-9]{4}|[A-Z]{6}[0-9]{2}[A-Z][0-9]{2}[A-Z][0-9]{3}[A-Z])(?![A-Za-z0-9])";
    internal const string _iban = @"(?<![A-Za-z0-9])(?:"
        + @"NO[0-9]{2}(?: ?[A-Z0-9]){11}|BE[0-9]{2}(?: ?[A-Z0-9]){12}|"
        + @"(?:DK|FO|FI|GL|NL|SD)[0-9]{2}(?: ?[A-Z0-9]){14}|"
        + @"(?:MK|SI)[0-9]{2}(?: ?[A-Z0-9]){15}|"
        + @"(?:AT|BA|EE|KZ|XK|LT|LU)[0-9]{2}(?: ?[A-Z0-9]){16}|"
        + @"(?:HR|LV|LI|CH)[0-9]{2}(?: ?[A-Z0-9]){17}|"
        + @"(?:BH|BG|CR|GE|DE|IE|ME|GB|VA)[0-9]{2}(?: ?[A-Z0-9]){18}|"
        + @"(?:GI|IL|TL|AE)[0-9]{2}(?: ?[A-Z0-9]){19}|"
        + @"(?:AD|CZ|MD|PK|RO|SA|SK|ES|SE|TN|VG)[0-9]{2}(?: ?[A-Z0-9]){20}|"
        + @"(?:PT|ST)[0-9]{2}(?: ?[A-Z0-9]){21}|(?:IS|TR)[0-9]{2}(?: ?[A-Z0-9]){22}|"
        + @"(?:FR|GR|IT|MR|MC|SM)[0-9]{2}(?: ?[A-Z0-9]){23}|"
        + @"(?:AL|AZ|CY|DO|GT|HU|LB|PL)[0-9]{2}(?: ?[A-Z0-9]){24}|"
        + @"(?:BR|PS|QA|UA)[0-9]{2}(?: ?[A-Z0-9]){25}|"
        + @"(?:JO|KW|MU)[0-9]{2}(?: ?[A-Z0-9]){26}|"
        + @"(?:MT|SC)[0-9]{2}(?: ?[A-Z0-9]){27}|LC[0-9]{2}(?: ?[A-Z0-9]){28}|"
        + @"[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30})(?![A-Za-z0-9])";
    internal const string _postalAddress = @"(?<![A-Za-z0-9])[0-9]{1,5}[ ]+(?:[A-Z][a-z]+[ ]+){1,4}(?:Street|St|Road|Rd|Avenue|Ave|Lane|Ln|Drive|Dr|Boulevard|Blvd)\b";

    internal static string _reservedValue(PersonalDataKind kind)
    {
        return kind switch
        {
            PersonalDataKind.Email => "jane.doe@example.com",
            PersonalDataKind.Phone => "+12025550100",
            PersonalDataKind.NationalIdentifier => "000-00-0000",
            PersonalDataKind.Iban => "GB00TEST00000000000000",
            PersonalDataKind.PostalAddress => "1 Example Street",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    internal static bool _isReserved(PersonalDataKind kind, string value)
    {
        switch (kind)
        {
            case PersonalDataKind.Email:
                var separator = value.LastIndexOf('@');
                var domain = separator < 0 ? value : value.Substring(separator + 1);
                return _isDomain(domain, "example.com") || _isDomain(domain, "example.org")
                    || _isDomain(domain, "example.net") || _isDomain(domain, "invalid")
                    || _isDomain(domain, "test") || _isDomain(domain, "example")
                    || _isDomain(domain, "localhost");
            case PersonalDataKind.Phone:
                var digits = _alphanumeric(value);
                return (digits.Length == 11 && digits[0] == '1'
                        && digits.Substring(4, 5) == "55501")
                    || (digits.Length == 10 && digits.Substring(3, 5) == "55501")
                    || (digits.Length == 12 && digits.StartsWith("447700900", StringComparison.Ordinal))
                    || (digits.Length == 8 && digits.StartsWith("155501", StringComparison.Ordinal));
            case PersonalDataKind.NationalIdentifier:
                return value is "000-00-0000" or "XXXXXX00X00X000X" or "XX00000000";
            case PersonalDataKind.Iban:
                return !_isChecksumValid(kind, value);
            case PersonalDataKind.PostalAddress:
                return value is "1 Example Street" or "2 Example Avenue" or "3 Example Road";
            default:
                return false;
        }
    }

    internal static bool _isChecksumValid(PersonalDataKind kind, string value)
    {
        if (kind == PersonalDataKind.Iban)
        {
            var compact = _alphanumeric(value);
            if (compact.Length < 15 || compact.Length > 34)
            {
                return false;
            }

            var remainder = 0;
            for (var index = 0; index < compact.Length; index++)
            {
                var character = compact[(index + 4) % compact.Length];
                if (character is >= '0' and <= '9')
                {
                    remainder = ((remainder * 10) + character - '0') % 97;
                }
                else if (character is >= 'A' and <= 'Z')
                {
                    remainder = ((remainder * 100) + character - 'A' + 10) % 97;
                }
                else
                {
                    return false;
                }
            }

            return remainder == 1;
        }

        if (kind == PersonalDataKind.NationalIdentifier)
        {
            if (value.Length == 11 && value[3] == '-' && value[6] == '-')
            {
                return !value.StartsWith("000", StringComparison.Ordinal)
                    && !value.StartsWith("666", StringComparison.Ordinal) && value[0] != '9'
                    && value.Substring(4, 2) != "00" && value.Substring(7) != "0000";
            }

            if (value.Length == 16)
            {
                const string oddLetters = "BAFHJNPRTVCESULDGIMOQKWZYX";
                const string oddDigits = "BAFHJNPRTV";
                var total = 0;
                for (var index = 0; index < 15; index++)
                {
                    var character = char.ToUpperInvariant(value[index]);
                    var number = character is >= '0' and <= '9' ? character - '0' : character - 'A';
                    if (number < 0 || number > 25)
                    {
                        return false;
                    }

                    total += index % 2 == 0
                        ? (character is >= '0' and <= '9' ? oddDigits[number] : oddLetters[number]) - 'A'
                        : number;
                }

                return char.ToUpperInvariant(value[15]) == 'A' + (total % 26);
            }
        }

        return true;
    }

    private static bool _isDomain(string domain, string reserved)
    {
        return string.Equals(domain, reserved, StringComparison.OrdinalIgnoreCase)
            || domain.EndsWith("." + reserved, StringComparison.OrdinalIgnoreCase);
    }

    private static string _alphanumeric(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Where(char.IsLetterOrDigit))
        {
            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }
}
