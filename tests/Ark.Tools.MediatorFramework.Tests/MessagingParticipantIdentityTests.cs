// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies participant-owned identity constants and normalization.</summary>
[TestClass]
public sealed class MessagingParticipantIdentityTests
{
    [TestMethod]
    public void NormalizeIdentityStripsTheParticipantSuffix()
    {
        MessagingParticipantAttribute.NormalizeIdentity("PrintingFunctionsParticipant")
            .Should().Be("printing-functions");
    }

    [TestMethod]
    public void NormalizeIdentityKeepsClassNamesWithoutTheSuffix()
    {
        MessagingParticipantAttribute.NormalizeIdentity("WebFrontend")
            .Should().Be("web-frontend");
    }

    [TestMethod]
    public void NormalizeIdentitySplitsAcronymBoundaries()
    {
        MessagingParticipantAttribute.NormalizeIdentity("PDFPrinterParticipant")
            .Should().Be("pdf-printer");
    }

    [TestMethod]
    public void IdentityClassSuffixIsACompileTimeConstant()
    {
        MessagingParticipantAttribute.IdentityClassSuffix.Should().Be("Participant");
    }
}
