// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.Auth;

public static class AuthClaims
{
    #if NET10_0_OR_GREATER
    [NotPersonalData("Claim-name URI used for authorization metadata, not a personal record.")]
    #endif
    public const string ArkEmailClaim = "http://ark-energy.eu/claims/email";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Claim-name token used for authorization metadata, not personal data.")]
    #endif
    public const string EmailsClaim = "emails";
}