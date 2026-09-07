// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using global::NLog.Common;
using global::NLog.Layouts;
using global::NLog.Targets;
using global::NLog.Targets.Wrappers;

namespace Ark.Tools.Compliance.NLog;

/// <summary>Applies last-resort pattern scanning to a target's rendered output.</summary>
[Target("ArkComplianceRedaction")]
public sealed class RedactingTargetWrapper : WrapperTargetBase
{
    private readonly ComplianceRedactor _redactor;
    private Layout? _originalLayout;
    private Layout? _wrappedLayout;

    /// <summary>Initializes a wrapper with a runtime redaction policy.</summary>
    /// <param name="target">The downstream target.</param>
    /// <param name="redactor">The immutable runtime redaction policy.</param>
    public RedactingTargetWrapper(Target target, ComplianceRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(redactor);
        WrappedTarget = target;
        _redactor = redactor;
    }

    /// <inheritdoc />
    protected override void InitializeTarget()
    {
        _wrapLayout();

        base.InitializeTarget();
    }

    /// <inheritdoc />
    protected override void CloseTarget()
    {
        if (_findTargetWithLayout(WrappedTarget) is { } targetWithLayout && _originalLayout is not null)
            targetWithLayout.Layout = _originalLayout;

        base.CloseTarget();
    }

    /// <inheritdoc />
    protected override void Write(AsyncLogEventInfo logEvent)
    {
        _wrapLayout();
        WrappedTarget!.WriteAsyncLogEvent(logEvent);
    }

    private void _wrapLayout()
    {
        if (_findTargetWithLayout(WrappedTarget) is not { } targetWithLayout
            || ReferenceEquals(targetWithLayout.Layout, _wrappedLayout))
            return;

        var originalLayout = targetWithLayout.Layout;
        _originalLayout = originalLayout;
        _wrappedLayout = Layout.FromMethod(
            logEvent => _redactor.Scan(originalLayout.Render(logEvent)),
            LayoutRenderOptions.ThreadAgnostic);
        targetWithLayout.Layout = _wrappedLayout;
    }

    private static TargetWithLayout? _findTargetWithLayout(Target? target)
    {
        while (target is WrapperTargetBase wrapper)
            target = wrapper.WrappedTarget;

        return target as TargetWithLayout;
    }
}
