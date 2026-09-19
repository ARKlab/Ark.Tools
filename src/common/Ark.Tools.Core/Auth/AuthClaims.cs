// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Ark.Tools.Compliance;

namespace Ark.Tools.Auth;

public static class AuthClaims
{
    [NotPersonalData("Claim-name URI used for authorization metadata, not a personal record.")]
    public const string ArkEmailClaim = "http://ark-energy.eu/claims/email";
    [NotPersonalData("Claim-name token used for authorization metadata, not personal data.")]
    public const string EmailsClaim = "emails";
}