// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Compliance;

using System.Net.Mail;

namespace Ark.Tools.Core;

public static class EmailValidator
{

    public static bool IsValid(
        [PersonalData] string emailAddress)
    {
        return MailAddress.TryCreate(emailAddress, out var _);
    }

}