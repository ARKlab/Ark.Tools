// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Net.Mail;
using System;
using System.Text.RegularExpressions;

namespace Ark.Tools.Core
{
    public static partial class EmailValidator
    {
        [Obsolete("IsValid() now uses MailAddress.TryCreate which is more performant. This property is going to be removed in next major")]
        public static Regex Regex { get; } = _validEmailAddressRegEx();

        public static bool IsValid(string emailAddress)
        {
#if NET6_0_OR_GREATER
            return MailAddress.TryCreate(emailAddress, out var _);
#else
            try
            {
                var _ = new MailAddress(emailAddress);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
#endif
        }

        private const string _regexRFC5322 = @"^(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*"")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])$";

#if NET8_0_OR_GREATER
        [GeneratedRegex(_regexRFC5322, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
        private static partial Regex _validEmailAddressRegEx();
#else
        private static Regex _validEmailAddressRegEx()
        {
            return new Regex(_regexRFC5322, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
        }
#endif

    }
}
