// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// <see cref="SmtpConnectionBuilder.ConnectionString"/> is a transport value: the ConfigurationManager
/// helper builds it from separate app settings and <c>NLogConfigurer</c> parses it back, so it must
/// round-trip. Masking belongs at the logging boundary, which the <c>[Secret]</c> annotation drives.
/// </summary>
[TestClass]
public class SmtpConnectionBuilderTests
{
    [TestMethod]
    public void ConnectionString_RoundTrips()
    {
        var built = new SmtpConnectionBuilder
        {
            Server = "smtp.sendgrid.net",
            Port = 587,
            Username = "gnegnegne",
            Password = "nonlosai",
            UseSsl = true,
            From = "noreply@example.com"
        }.ConnectionString;

        var parsed = new SmtpConnectionBuilder(built);

        parsed.Server.Should().Be("smtp.sendgrid.net");
        parsed.Port.Should().Be(587);
        parsed.Username.Should().Be("gnegnegne");
        parsed.Password.Should().Be("nonlosai");
        parsed.UseSsl.Should().BeTrue();
        parsed.From.Should().Be("noreply@example.com");
    }

    [TestMethod]
    public void ConnectionString_RoundTrips_WhenOptionalValuesAreMissing()
    {
        var built = new SmtpConnectionBuilder { Server = "localhost" }.ConnectionString;

        var parsed = new SmtpConnectionBuilder(built);

        parsed.Server.Should().Be("localhost");
        parsed.Port.Should().Be(25);
        parsed.Username.Should().BeNull();
        parsed.Password.Should().BeNull();
    }
}
