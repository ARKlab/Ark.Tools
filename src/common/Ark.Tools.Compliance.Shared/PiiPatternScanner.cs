// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.Internal;

using System.Buffers;
using System.Text.RegularExpressions;

namespace Ark.Tools.Compliance.Shared;

internal static partial class PiiPatternScanner
{
    private const string _pattern =
        "(?<Email>" + PersonalDataPatterns._email + ")"
        + "|(?<Phone>" + PersonalDataPatterns._phone + ")"
        + "|(?<NationalIdentifier>" + PersonalDataPatterns._nationalIdentifier + ")"
        + "|(?<Iban>" + PersonalDataPatterns._iban + ")"
        + "|(?<PostalAddress>" + PersonalDataPatterns._postalAddress + ")";
    private static readonly SearchValues<char> _candidates = SearchValues.Create("@0123456789");

    [GeneratedRegex(_pattern, RegexOptions.CultureInvariant, 25)]
    private static partial Regex _regex();

    internal static string _redact(string value, string replacement)
    {
        if (!value.AsSpan().ContainsAny(_candidates))
            return value;
        try
        {
            StringBuilder? builder = null;
            var previous = 0;
            for (var match = _regex().Match(value); match.Success; match = match.NextMatch())
            {
                var kind = match.Groups["Iban"].Success ? PersonalDataKind.Iban
                    : match.Groups["NationalIdentifier"].Success ? PersonalDataKind.NationalIdentifier
                    : PersonalDataKind.Email;
                if (!PersonalDataPatterns._isChecksumValid(kind, match.Value))
                    continue;
                builder ??= new StringBuilder(value.Length);
                builder.Append(value, previous, match.Index - previous);
                builder.Append(replacement);
                previous = match.Index + match.Length;
                if (!value.AsSpan(previous).ContainsAny(_candidates))
                    break;
            }
            if (builder is null)
                return value;
            builder.Append(value, previous, value.Length - previous);
            return builder.ToString();
        }
        catch (RegexMatchTimeoutException)
        {
            return replacement;
        }
    }
}
