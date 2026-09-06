// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog;
using global::NLog.Config;
using global::NLog.Targets;

using System.Runtime.CompilerServices;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Configures runtime redaction for NLog.</summary>
public static class ComplianceNLogExtensions
{
    private static readonly ConditionalWeakTable<LoggingConfiguration, Policy> _policies = new();
    private static readonly ComplianceRedactor _defaultPolicy = new();
    private static IValueFormatter? _originalFormatter;

    /// <summary>Overrides redaction applied by the specified NLog configuration.</summary>
    /// <param name="configuration">The NLog configuration to update.</param>
    /// <param name="configure">Optional overrides of fail-closed defaults.</param>
    /// <returns>The original NLog configuration.</returns>
    public static LoggingConfiguration WithComplianceRedaction(
        this LoggingConfiguration configuration,
        Action<ComplianceRedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new ComplianceRedactionOptions();
        configure?.Invoke(options);
        _setPolicy(configuration, new ComplianceRedactor(options));
        return configuration;
    }

    /// <summary>Explicitly disables runtime redaction for this NLog configuration.</summary>
    /// <remarks>Generated sensitive values retain their own safe <c>ToString</c> behavior.</remarks>
    /// <param name="configuration">The NLog configuration to update.</param>
    /// <returns>The original NLog configuration.</returns>
    public static LoggingConfiguration WithoutComplianceRedaction(this LoggingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _setPolicy(configuration, null);
        return configuration;
    }

    /// <summary>Applies the default or explicitly selected redaction policy.</summary>
    /// <param name="configuration">The NLog configuration to protect.</param>
    public static void ConfigureCompliance(this LoggingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var redactor = _policies.TryGetValue(configuration, out var policy) ? policy.Redactor : _defaultPolicy;
        _originalFormatter ??= (IValueFormatter)LogManager.LogFactory.ServiceRepository.GetService(typeof(IValueFormatter));
        LogManager.Setup().SetupSerialization(builder =>
        {
            builder.RegisterValueFormatter(redactor is null
                ? _originalFormatter
                : new ComplianceValueFormatter(_originalFormatter, redactor));
            builder.RegisterObjectTransformation<IRuntimeClassifiedValue>(value =>
                redactor is null ? value : redactor.Redact(value)!);
        });

        var cache = new RedactedEventCache(redactor);
        var wrappers = new Dictionary<Target, Target>();
        foreach (var rule in configuration.LoggingRules)
            _wrapRule(rule, configuration, cache, wrappers, redactor is not null);
    }

    private static void _setPolicy(LoggingConfiguration configuration, ComplianceRedactor? redactor)
    {
        _policies.Remove(configuration);
        _policies.Add(configuration, new(redactor));
    }

    private static void _wrapRule(
        LoggingRule rule,
        LoggingConfiguration configuration,
        RedactedEventCache cache,
        Dictionary<Target, Target> wrappers,
        bool enabled)
    {
        for (var i = 0; i < rule.Targets.Count; i++)
        {
            var target = rule.Targets[i];
            if (enabled && target is RedactingTargetWrapper)
            {
                ((RedactingTargetWrapper)target)._setCache(cache);
                continue;
            }
            while (target is RedactingTargetWrapper existing)
            {
                target = existing.WrappedTarget ?? throw new InvalidOperationException("A redaction wrapper requires a downstream target.");
            }
            if (!enabled)
            {
                rule.Targets[i] = target;
                continue;
            }
            if (!wrappers.TryGetValue(target, out var wrapper))
            {
                var wrapperName = target.Name + ".Compliance";
                wrapper = configuration.FindTargetByName(wrapperName) is RedactingTargetWrapper existing
                    && ReferenceEquals(existing.WrappedTarget, target)
                    ? existing
                    : new RedactingTargetWrapper(target, cache) { Name = wrapperName };
                if (wrapper is RedactingTargetWrapper existingWrapper)
                    existingWrapper._setCache(cache);
                wrappers.Add(target, wrapper);
                if (!ReferenceEquals(configuration.FindTargetByName(wrapper.Name), wrapper))
                    configuration.AddTarget(wrapper);
            }
            rule.Targets[i] = wrapper;
        }
#pragma warning disable CS0618 // Legacy child rules must also be protected before any target receives the event.
        foreach (var child in rule.ChildRules)
            _wrapRule(child, configuration, cache, wrappers, enabled);
#pragma warning restore CS0618
    }

    private sealed record Policy(ComplianceRedactor? Redactor);
}
