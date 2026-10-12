// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using AwesomeAssertions;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

[TestClass]
public sealed class ComplianceRedactionProcessorTests
{
    [TestMethod]
    public void OnEnd_RedactsOnlyStringTagsThatContainPii()
    {
        using var activity = new Activity("operation");
        activity.SetTag("plain", "hello");
        activity.SetTag("email", "mail user@example.com now");
        activity.SetTag("count", 3);
        using var processor = new ArkComplianceRedactionProcessor(
            new ComplianceRedactionOptions { PiiScan = PiiScanMode.MessageAndProperties });

        processor.OnEnd(activity);

        activity.GetTagItem("plain").Should().Be("hello");
        // The scanner redacts the whole value when its regex times out (e.g. on a cold runner), so only assert the PII is gone.
        activity.GetTagItem("email").Should().BeOfType<string>()
            .Which.Should().Contain(PiiScanner.Marker).And.NotContain("user@example.com");
        activity.GetTagItem("count").Should().Be(3);
        activity.TagObjects.Should().HaveCount(3);
    }
}
