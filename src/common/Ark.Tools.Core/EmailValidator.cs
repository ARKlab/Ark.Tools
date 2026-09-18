// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using System.Net.Mail;

namespace Ark.Tools.Core;

public static class EmailValidator
{

    public static bool IsValid(
#if NET10_0_OR_GREATER
    [PersonalData]
#endif
 string emailAddress)
    {
        return MailAddress.TryCreate(emailAddress, out var _);
    }

}