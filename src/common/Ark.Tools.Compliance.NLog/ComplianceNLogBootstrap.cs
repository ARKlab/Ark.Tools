// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.NLog;

using global::NLog;
using global::NLog.Config;
using global::NLog.Targets;

using System.Runtime.CompilerServices;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Connects the optional compliance package to Ark's default NLog setup.</summary>
public static class ComplianceNLogBootstrap
{
    private static readonly ConditionalWeakTable<NLogConfigurer.Configurer, Policy> _policies = new();
    private static readonly ComplianceRedactor _defaultPolicy = new();
    private static IValueFormatter? _originalFormatter;

    /// <summary>Registers the default-on hook used by <see cref="NLogConfigurer"/>.</summary>
    public static void Initialize()
    {
        NLogConfigurer.RegisterComplianceConfiguration(_configure);
    }

    internal static void _setPolicy(NLogConfigurer.Configurer configurer, ComplianceRedactor? redactor)
    {
        Initialize();
        _policies.Remove(configurer);
        _policies.Add(configurer, new(redactor));
    }

    private static void _configure(NLogConfigurer.Configurer configurer, LoggingConfiguration configuration)
    {
        var redactor = _policies.TryGetValue(configurer, out var policy) ? policy.Redactor : _defaultPolicy;
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
            while (target is RedactingTargetWrapper existing)
                target = existing.WrappedTarget ?? throw new InvalidOperationException("A redaction wrapper requires a downstream target.");
            if (!enabled)
            {
                rule.Targets[i] = target;
                continue;
            }
            if (!wrappers.TryGetValue(target, out var wrapper))
            {
                wrapper = new RedactingTargetWrapper(target, cache)
                {
                    Name = target.Name + ".Compliance",
                };
                wrappers.Add(target, wrapper);
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
