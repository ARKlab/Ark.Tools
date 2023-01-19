// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Text.RegularExpressions;

namespace Ark.Tools.Core
{
    public static class EmailValidator
    {
        public static Regex Regex { get; } = _validEmailAddressRegEx();

        public static bool IsValid(string emailAddress)
        {
            return Regex.IsMatch(emailAddress);
        }

        private static Regex _validEmailAddressRegEx()
        {
            string qtext = "[^\\x0d\\x22\\x5c\\x80-\\xff]"; // <any CHAR excepting <">, "\" & CR, and including linear-white-space>
            string dtext = "[^\\x0d\\x5b-\\x5d\\x80-\\xff]"; // <any CHAR excluding "[", "]", "\" & CR, & including linear-white-space>
            string atom = "[^\\x00-\\x20\\x22\\x28\\x29\\x2c\\x2e\\x3a-\\x3c\\x3e\\x40\\x5b-\\x5d\\x7f-\\xff]+"; // *<any CHAR except specials, SPACE and CTLs>
            string quoted_pair = "\\x5c[\\x00-\\x7f]"; // "\" CHAR 
            string quoted_string = $"\\x22({qtext}|{quoted_pair})*\\x22"; // <"> *(qtext/quoted-pair) <">
            string word = $"({atom}|{quoted_string})"; //atom / quoted-string
            string domain_literal = $"\\x5b({dtext}|{quoted_pair})*\\x5d"; // "[" *(dtext / quoted-pair) "]"
            string domain_ref = atom; // atom 
            string sub_domain = string.Format("({0}|{1})", domain_ref, domain_literal); // domain-ref / domain-literal
            string domain = string.Format("{0}(\\x2e{0})*", sub_domain); // sub-domain *("." sub-domain)
            string local_part = string.Format("{0}(\\x2e{0})*", word); // word *("." word) 
            string addr_spec = string.Format("{0}\\x40{1}", local_part, domain); //local-part "@" domain
            return new Regex(string.Format("^{0}$", addr_spec), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
        }
    }
}
