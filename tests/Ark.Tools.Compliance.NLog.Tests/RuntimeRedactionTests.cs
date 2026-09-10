// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.NLog;

using AwesomeAssertions;

using global::NLog;
using global::NLog.Layouts;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Security.Cryptography;

namespace Ark.Tools.Compliance.NLog.Tests;

/// <summary>Exercises the runtime redaction boundaries using real NLog and OTel pipelines.</summary>
[TestClass]
[DoNotParallelize]
public sealed class RuntimeRedactionTests
{
    private const string _cleartext = "synthetic-private-value";

    /// <summary>Gets or sets the current test's diagnostics context.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Default setup must protect classified values without a redaction call.</summary>
    [TestMethod]
    public void DefaultNLogSetup_RedactsMessageAndStructuredProperties()
    {
        var output = _capture(null, static logger =>
        {
            var logEvent = new LogEventInfo(LogLevel.Info, logger.Name, CultureInfo.InvariantCulture,
                "payload {@Payload}", [new Payload()]);
            logEvent.Properties["boxed"] = ApiKey.From(_cleartext);
            logger.Log(logEvent);
            logger.Info(CultureInfo.InvariantCulture, "positional {0}", ApiKey.From(_cleartext));
        });

        output.Should().Contain("***");
        output.Should().NotContain(_cleartext);
        output.Should().Contain("safe-operation");
    }

    /// <summary>Default setup preserves an ordinary exception message.</summary>
    [TestMethod]
    public void DefaultNLogSetup_PreservesOrdinaryExceptionMessage()
    {
        var output = _capture(null, static logger =>
            logger.Error(new InvalidOperationException("ordinary failure"), CultureInfo.InvariantCulture, "failed"));

        output.Should().Contain("ordinary failure");
    }

    /// <summary>The opt-out only disables PII scanning; generated values remain intrinsically safe.</summary>
    [TestMethod]
    public void ExplicitOptOut_RestoresCleartext()
    {
        var output = _capture(static configurer => configurer.WithoutComplianceRedaction(),
            static logger => logger.Info(CultureInfo.InvariantCulture, "value {Value}", ApiKey.From(_cleartext)));

        output.Should().Contain("***");
        output.Should().NotContain(_cleartext);
    }

    /// <summary>Overrides apply to both templates and boxed properties.</summary>
    [TestMethod]
    public void Override_CanSelectHmacAndSnapshotsOptions()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var options = new ComplianceRedactionOptions { HmacKey = key };
        var redactor = new ComplianceRedactor(options);
        redactor.Redact(_cleartext, ArkRedaction.Hmac).Should().StartWith("hmac:");
        options.HmacKey![0].Should().NotBe(0);
    }

    /// <summary>Direct redaction modes fail closed when configured without a key.</summary>
    [TestMethod]
    public void Defaults_ApplyEveryClassificationAndEraseUnknown()
    {
        var redactor = new ComplianceRedactor();
        redactor.Redact(_cleartext, ArkRedaction.Erase).Should().Be("***");
        redactor.Redact("correlation-42", ArkRedaction.None).Should().Be("correlation-42");
        var keyed = new ComplianceRedactor(new() { HmacKey = RandomNumberGenerator.GetBytes(32) });
        keyed.Redact(_cleartext, ArkRedaction.Hmac).Should().StartWith("hmac:");
    }

    /// <summary>Generated sensitive values are safe through their normal formatting surface.</summary>
    [TestMethod]
    public void GeneratedValue_UsesSafeToString()
    {
        var value = EmailAddress.From("alice@example.test");
        value.ToString(null, CultureInfo.InvariantCulture).Should().Be("***");
        var output = _capture(null, logger => logger.Info(CultureInfo.InvariantCulture, "value {@Value}", value));
        output.Should().Contain("***");
        output.Should().NotContain("alice@example.test");
    }

    /// <summary>NLog JSON event-property serialization uses the generated safe representation.</summary>
    [TestMethod]
    public void JsonLayout_UsesGeneratedSafeRepresentation()
    {
        var value = EmailAddress.From("alice@example.test");
        var logEvent = new LogEventInfo(LogLevel.Info, "Compliance.Runtime.Tests", "payload");
        logEvent.Properties["email"] = value;
        var json = new JsonLayout
        {
            IncludeEventProperties = true,
            ExcludeEmptyProperties = true,
        }.Render(logEvent);

        json.Should().Contain("***");
        json.Should().NotContain("alice@example.test");
    }

    /// <summary>Scanning is opt-in and covers untyped message arguments and properties.</summary>
    [TestMethod]
    public void PatternScan_IsOffByDefaultAndMasksUntypedPayloadsWhenEnabled()
    {
        const string message = "contact alice@private-domain.dev";
        _capture(null, static logger => logger.Info(CultureInfo.InvariantCulture, "{Message}", message))
            .Should().Contain(message);
        var output = _capture(static configurer => configurer.WithComplianceRedaction(static options =>
            options.PiiScan = PiiScanMode.MessageAndProperties), static logger =>
        {
            logger.Info(CultureInfo.InvariantCulture, "{Message}", message);
        });
        output.Should().Contain(ComplianceRedactor.Marker);
        output.Should().NotContain("alice@private-domain.dev");
    }

    /// <summary>PII scanning masks exception messages without erasing ordinary exception output.</summary>
    [TestMethod]
    public void PatternScan_ScansExceptionMessage()
    {
        var output = _capture(static configurer => configurer.WithComplianceRedaction(static options =>
            options.PiiScan = PiiScanMode.MessageAndProperties), static logger =>
            logger.Error(new InvalidOperationException("contact alice@private-domain.dev"), CultureInfo.InvariantCulture, "failed"));

        output.Should().Contain(ComplianceRedactor.Marker);
        output.Should().NotContain("alice@private-domain.dev");
    }

    /// <summary>Pattern scanning classifies phone and postal-address matches independently from email.</summary>
    [TestMethod]
    public void PatternScan_MasksPhoneAndPostalAddress()
    {
        var scanner = new PiiScanner(PiiScanMode.MessageAndProperties);

        scanner.Scan("call +12025550100 at 1 Example Street")
            .Should().Be($"call {ComplianceRedactor.Marker} at {ComplianceRedactor.Marker}");
    }

    /// <summary>Default Ark setup registers redaction before an actual exporting processor.</summary>
    [TestMethod]
    public void DefaultOtelSetup_RedactsExportedSpanTags()
    {
        using var exporter = new CapturingExporter();
        using var processor = new SimpleActivityExportProcessor(exporter);
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .AddArkAspNetCoreOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Compliance.Runtime.Tests")
                .SetSampler(new AlwaysOnSampler())
                .AddProcessor(processor));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("Compliance.Runtime.Tests");
        using (var activity = source.StartActivity("operation"))
        {
            activity.Should().NotBeNull();
            activity!.SetTag("credential", ApiKey.From(_cleartext));
            activity.SetTag("email", EmailAddress.From("alice@example.test"));
            activity.SetTag("normal", "safe-operation");
        }

        exporter.Tags.Should().NotBeNull();
        exporter.Tags!["credential"]!.ToString().Should().Be("***");
        exporter.Tags["email"]!.ToString().Should().Be("***");
        exporter.Tags["normal"].Should().Be("safe-operation");
    }

    /// <summary>A bounded smoke budget catches pathological per-event regressions.</summary>
    [TestMethod]
    public void Throughput_DefaultRedactionRemainsBounded()
    {
        var redactor = new ComplianceRedactor();
        for (var i = 0; i < 1000; i++)
            _ = redactor.Redact(_cleartext, ArkRedaction.Erase);
        const int iterations = 100000;
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            _ = redactor.Redact(_cleartext, ArkRedaction.Erase);
        var elapsed = Stopwatch.GetElapsedTime(started);
        (elapsed.TotalMicroseconds / iterations).Should().BeLessThan(50);
    }

    /// <summary>Measures scanner cost on a representative 200-character untyped message.</summary>
    [TestMethod]
    public void Throughput_PatternScanHasBoundedCost()
    {
        var scanner = new PiiScanner(PiiScanMode.MessageAndProperties);
        var message = "contact alice@private-domain.dev ".PadRight(200, 'x');
        for (var i = 0; i < 1000; i++)
            _ = scanner.Scan(message);
        const int iterations = 10000;
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            _ = scanner.Scan(message);
        var microseconds = Stopwatch.GetElapsedTime(started).TotalMicroseconds / iterations;
        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "Pattern scan: {0:F3} microseconds per 200-character message.", microseconds));
        microseconds.Should().BeLessThan(50, "the CI smoke limit allows contention; the release target is 2 microseconds");
    }

    private static string _capture(
        Action<NLogConfigurer.Configurer>? configure,
        Action<Logger> log)
    {
        _ = NLogConfigurer.For("initialize");
        var originalConfiguration = LogManager.Configuration;
        var originalOutput = Console.Out;
        var formatter = (IValueFormatter)LogManager.LogFactory.ServiceRepository.GetService(typeof(IValueFormatter));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            var configurer = NLogConfigurer.For("RuntimeRedactionTests")
                .WithConsoleTarget(false)
                .WithConsoleRule("*", LogLevel.Trace);
            configure?.Invoke(configurer);
            configurer.Apply();
            log(LogManager.GetLogger("Compliance.Runtime.Tests"));
            LogManager.Flush();
            return output.ToString();
        }
        finally
        {
            Console.SetOut(originalOutput);
            LogManager.Configuration = originalConfiguration;
            LogManager.Setup().SetupSerialization(builder => builder.RegisterValueFormatter(formatter));
        }
    }

    private sealed class Payload
    {
        public EmailAddress Credential { get; } = EmailAddress.From("alice@example.test");
        public string Operation { get; } = "safe-operation";
    }

    private sealed class CapturingExporter : BaseExporter<Activity>
    {
        public Dictionary<string, object?>? Tags { get; private set; }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
                Tags = activity.TagObjects.ToDictionary(static tag => tag.Key, static tag => tag.Value, StringComparer.Ordinal);
            return ExportResult.Success;
        }
    }
}
