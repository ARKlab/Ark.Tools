// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.NLog;

using AwesomeAssertions;

using global::NLog;
using global::NLog.Targets;

using Microsoft.Extensions.Compliance.Classification;
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
            logEvent.Properties["boxed"] = new SecretValue();
            logger.Log(logEvent);
        });

        output.Should().Contain(ComplianceRedactor.Marker);
        output.Should().NotContain(SecretValue.Cleartext);
        output.Should().Contain("safe-operation");
    }

    /// <summary>The opt-out restores ordinary classified values, not generated safe ToString methods.</summary>
    [TestMethod]
    public void ExplicitOptOut_RestoresCleartext()
    {
        var output = _capture(static configurer => configurer.WithoutComplianceRedaction(),
            static logger => logger.Info(CultureInfo.InvariantCulture, "value {Value}", new SecretValue()));

        output.Should().Contain(SecretValue.Cleartext);
    }

    /// <summary>Overrides apply to both templates and boxed properties.</summary>
    [TestMethod]
    public void Override_CanSelectHmacAndSnapshotsOptions()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var options = new ComplianceRedactionOptions { HmacKey = key };
        options.For(ArkDataClassifications.Secret, ArkRedaction.Hmac);
        var redactor = new ComplianceRedactor(options);
        options.For(ArkDataClassifications.Secret, ArkRedaction.None);
        redactor.Redact(new SecretValue()).Should().BeOfType<string>().Which.Should().StartWith("hmac:");

        var output = _capture(configurer => configurer.WithComplianceRedaction(options =>
        {
            options.HmacKey = key;
            options.For(ArkDataClassifications.Secret, ArkRedaction.Hmac);
        }), static logger => logger.Info(CultureInfo.InvariantCulture, "value {Value}", new SecretValue()));

        output.Should().Contain("hmac:");
        output.Should().NotContain(SecretValue.Cleartext);
    }

    /// <summary>All classifications use fail-closed defaults, including missing HMAC keys.</summary>
    [TestMethod]
    public void Defaults_ApplyEveryClassificationAndEraseUnknown()
    {
        var redactor = new ComplianceRedactor();
        foreach (var classification in new[]
        {
            ArkDataClassifications.PersonalData,
            ArkDataClassifications.SensitivePersonalData,
            ArkDataClassifications.Secret,
            new DataClassification("Other", "Unknown"),
        })
        {
            redactor.Redact(SecretValue.Cleartext, classification).Should().Be(ComplianceRedactor.Marker);
        }
        redactor.Redact("correlation-42", ArkDataClassifications.Pseudonymous).Should().Be("correlation-42");
        var keyed = new ComplianceRedactor(new() { HmacKey = RandomNumberGenerator.GetBytes(32) });
        keyed.Redact(SecretValue.Cleartext, ArkDataClassifications.PersonalData).Should().StartWith("hmac:");
    }

    /// <summary>Generated sensitive values retain classification without reflection.</summary>
    [TestMethod]
    public void GeneratedValue_UsesRuntimeHmacInsteadOfToString()
    {
        var value = EmailAddress.From("alice@example.test");
        var redactor = new ComplianceRedactor(new() { HmacKey = RandomNumberGenerator.GetBytes(32) });
        redactor.Redact(value).Should().BeOfType<string>().Which.Should().StartWith("hmac:");
        var output = _capture(null, logger => logger.Info(CultureInfo.InvariantCulture, "value {@Value}", value));
        output.Should().Contain(ComplianceRedactor.Marker);
        output.Should().NotContain("alice@example.test");
    }

    /// <summary>Scanning is opt-in and covers untyped message arguments and properties.</summary>
    [TestMethod]
    public void PatternScan_IsOffByDefaultAndMasksUntypedPayloadsWhenEnabled()
    {
        const string message = "contact alice@private-domain.dev";
        _capture(null, static logger => logger.Info(CultureInfo.InvariantCulture, "{Message}", message))
            .Should().Contain(message);
        var output = _capture(static configurer => configurer.WithComplianceRedaction(static options =>
            options.PatternScan = PatternScanMode.MessageAndProperties), static logger =>
        {
            var logEvent = new LogEventInfo(LogLevel.Info, logger.Name, message);
            logEvent.Properties["contact"] = message;
            logger.Log(logEvent);
        });
        output.Should().Contain(ComplianceRedactor.Marker);
        output.Should().NotContain("alice@private-domain.dev");
    }

    /// <summary>Broken getters and cycles do not leak or prevent logging.</summary>
    [TestMethod]
    public void UnsafeObjectGraphs_FailClosed()
    {
        var redactor = new ComplianceRedactor();
        redactor.Redact(new ThrowingPayload()).Should().Be(ComplianceRedactor.Marker);
        var cycle = new Dictionary<string, object?>(StringComparer.Ordinal);
        cycle["self"] = cycle;
        redactor.Redact(cycle).Should().NotBeNull();
        redactor.Redact(new UnknownClassified()).Should().Be(ComplianceRedactor.Marker);
    }

    /// <summary>Pre-rendered input cannot reuse a cached cleartext message downstream.</summary>
    [TestMethod]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "LogFactory owns and disposes its configured targets.")]
    public void Wrapper_DiscardsCachedMessageAndDoesNotMutateCaller()
    {
        using var factory = new LogFactory();
        var memory = new MemoryTarget { Layout = "${message}|${all-event-properties}" };
        var wrapper = new RedactingTargetWrapper(memory);
        factory.Configuration = new();
        factory.Configuration.AddRuleForAllLevels(wrapper);
        factory.ReconfigExistingLoggers();
        var value = new SecretValue();
        var logEvent = new LogEventInfo(LogLevel.Info, "test", CultureInfo.InvariantCulture, "{Value}", [value]);
        logEvent.MessageFormatter = static logEvent => ((SecretValue)logEvent.Parameters![0]!).ToString();
        logEvent.Properties["value"] = value;
        logEvent.FormattedMessage.Should().Contain(SecretValue.Cleartext);
        factory.GetLogger("test").Log(logEvent);

        memory.Logs.Should().ContainSingle().Which.Should().Contain(ComplianceRedactor.Marker);
        memory.Logs[0].Should().NotContain(SecretValue.Cleartext);
        logEvent.Parameters![0].Should().BeSameAs(value);
        logEvent.Properties["value"].Should().BeSameAs(value);
    }

    /// <summary>Default Ark setup registers redaction before an actual exporting processor.</summary>
    [TestMethod]
    public void DefaultOtelSetup_RedactsExportedSpanTags()
    {
        using var exporter = new CapturingExporter();
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .AddArkAspNetCoreOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Compliance.Runtime.Tests")
                .SetSampler(new AlwaysOnSampler())
                .AddProcessor(new SimpleActivityExportProcessor(exporter)));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("Compliance.Runtime.Tests");
        using (var activity = source.StartActivity("operation"))
        {
            activity.Should().NotBeNull();
            activity!.SetTag("credential", new SecretValue());
            activity.SetTag("email", EmailAddress.From("alice@example.test"));
            activity.SetTag("normal", "safe-operation");
        }

        exporter.Tags.Should().NotBeNull();
        exporter.Tags!["credential"].Should().Be(ComplianceRedactor.Marker);
        exporter.Tags["email"].Should().Be(ComplianceRedactor.Marker);
        exporter.Tags["normal"].Should().Be("safe-operation");
    }

    /// <summary>A bounded smoke budget catches pathological per-event regressions.</summary>
    [TestMethod]
    public void Throughput_DefaultRedactionRemainsBounded()
    {
        var redactor = new ComplianceRedactor();
        var payload = new SecretValue();
        for (var i = 0; i < 1000; i++)
            _ = redactor.Redact(payload);
        const int iterations = 100000;
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            _ = redactor.Redact(payload);
        var elapsed = Stopwatch.GetElapsedTime(started);
        (elapsed.TotalMicroseconds / iterations).Should().BeLessThan(50);
    }

    /// <summary>Measures scanner cost on a representative 200-character untyped message.</summary>
    [TestMethod]
    public void Throughput_PatternScanHasBoundedCost()
    {
        var redactor = new ComplianceRedactor(new() { PatternScan = PatternScanMode.MessageAndProperties });
        var message = "contact alice@private-domain.dev ".PadRight(200, 'x');
        for (var i = 0; i < 1000; i++)
            _ = redactor.Scan(message);
        const int iterations = 10000;
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            _ = redactor.Scan(message);
        var microseconds = Stopwatch.GetElapsedTime(started).TotalMicroseconds / iterations;
        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "Pattern scan: {0:F3} microseconds per 200-character message.", microseconds));
        microseconds.Should().BeLessThan(50, "the CI smoke limit allows contention; the release target is 2 microseconds");
    }

    /// <summary>Measures added cost at the real NLog target boundary.</summary>
    [TestMethod]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "LogFactory owns and disposes its targets.")]
    public void Throughput_WrapperHasBoundedPerEventCost()
    {
        using var baseline = new LogFactory();
        using var protectedFactory = new LogFactory();
        baseline.Configuration = new();
        baseline.Configuration.AddRuleForAllLevels(new NullTarget { FormatMessage = true });
        protectedFactory.Configuration = new();
        protectedFactory.Configuration.AddRuleForAllLevels(new RedactingTargetWrapper(new NullTarget { FormatMessage = true }));
        baseline.ReconfigExistingLoggers();
        protectedFactory.ReconfigExistingLoggers();
        var clearLogger = baseline.GetLogger("baseline");
        var safeLogger = protectedFactory.GetLogger("protected");
        const int iterations = 20000;
        for (var i = 0; i < 1000; i++)
        {
            clearLogger.Info(CultureInfo.InvariantCulture, "event {Count}", i);
            safeLogger.Info(CultureInfo.InvariantCulture, "event {Count}", i);
        }
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            clearLogger.Info(CultureInfo.InvariantCulture, "event {Count}", i);
        var clear = Stopwatch.GetElapsedTime(started);
        started = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
            safeLogger.Info(CultureInfo.InvariantCulture, "event {Count}", i);
        var added = (Stopwatch.GetElapsedTime(started) - clear).TotalMicroseconds / iterations;
        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "NLog wrapper: {0:F3} added microseconds per event.", added));
        added.Should().BeLessThan(50);
    }

    private static string _capture(Action<NLogConfigurer.Configurer>? configure, Action<Logger> log)
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
            var target = LogManager.Configuration!.FindTargetByName<ConsoleTarget>(NLogConfigurer.ConsoleTarget);
            target!.Layout = "${message}|${all-event-properties}";
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

    [Secret]
    private sealed class SecretValue
    {
        public const string Cleartext = "synthetic-private-value";

        public override string ToString()
        {
            return Cleartext;
        }
    }

    private sealed class Payload
    {
        [Secret]
        public string Credential { get; } = SecretValue.Cleartext;
        public string Operation { get; } = "safe-operation";
    }

    private sealed class ThrowingPayload
    {
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Exercises an instance getter that fails during payload inspection.")]
        public string Value => throw new InvalidOperationException("Synthetic failure");
    }

    [UnknownClassification]
    private sealed class UnknownClassified
    {
        public override string ToString()
        {
            return SecretValue.Cleartext;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class UnknownClassificationAttribute : DataClassificationAttribute
    {
        public UnknownClassificationAttribute()
            : base(new("Other", "Unknown"))
        {
        }
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
