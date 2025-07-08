﻿using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

using Slack.Webhooks;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.NLog.Slack
{
    [Target(NLogConfigurer.SlackTarget)]
    public class SlackTarget : TargetWithContext
    {
        public string? WebHookUrl { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "CloseTarget() is called during Dispose() by NLog")]
        private SlackClient? _client = null;

        public SlackTarget() : base()
        {
            Layout = "${message}";
        }

        public override IList<TargetPropertyWithContext> ContextProperties { get; } = new List<TargetPropertyWithContext>();

        [ArrayParameter(typeof(TargetPropertyWithContext), "field")]
        public IList<TargetPropertyWithContext> Fields => ContextProperties;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MA0015:Specify the parameter name in ArgumentException", Justification = "Params are injected by NLog")]
        protected override void InitializeTarget()
        {
            if (String.IsNullOrWhiteSpace(this.WebHookUrl))
                throw new ArgumentOutOfRangeException(nameof(WebHookUrl), "Webhook URL cannot be empty.");

            if (!Uri.TryCreate(this.WebHookUrl, UriKind.Absolute, out var _))
                throw new ArgumentOutOfRangeException(nameof(WebHookUrl), "Webhook URL is an invalid URL.");

            _client = new SlackClient(this.WebHookUrl);

            if (this.ContextProperties.Count == 0)
            {
                this.ContextProperties.Add(new TargetPropertyWithContext("TimestampUtc", "${date:universalTime=true}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("LogLevel", "${level:uppercase=true}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("Host", "${machinename}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("Logger", "${logger}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("AppName", "${scopeproperty:item=AppName:whenempty=${gdc:item=AppName}}"));
            }

            base.InitializeTarget();
        }

        protected override void Write(AsyncLogEventInfo info)
        {
            try
            {
                this._sendToSlack(info);
                info.Continuation(null);
            }
            catch (Exception e)
            {
                info.Continuation(e);
            }
        }

        private void _sendToSlack(AsyncLogEventInfo info)
        {
            var message = RenderLogEvent(Layout, info.LogEvent);

            var slack = SlackMessageBuilder
                .Build()
                .OnError(e => info.Continuation(e))
                .WithMessage(message);

            var color = this._getSlackColorFromLogLevel(info.LogEvent.Level);

            if (this.ShouldIncludeProperties(info.LogEvent) || this.ContextProperties.Count > 0)
            {
                var allProperties = this.GetAllProperties(info.LogEvent)
                    .Where(w => !string.IsNullOrEmpty(w.Key) && !string.IsNullOrEmpty(w.Value?.ToString()))
                    .Select(s => (s.Key, s.Value?.ToString()));

                slack.AddAttachment(color, allProperties);
            }

            var exception = info.LogEvent.Exception;
            if (exception != null)
            {
                slack.AddAttachment(exception.Message, color, [($"Type: {exception.GetType()}", exception.StackTrace ?? "N/A")]);
            }

            slack.Send(_client ?? throw new InvalidOperationException("SlackClient is null"));
        }

        private string _getSlackColorFromLogLevel(LogLevel level)
        {
            if (_logLevelSlackColorMap.TryGetValue(level, out var color))
                return color;
            else
                return "#cccccc";
        }

        private static readonly Dictionary<LogLevel, string> _logLevelSlackColorMap = new()
        {
            { LogLevel.Warn, "warning" },
            { LogLevel.Error, "danger" },
            { LogLevel.Fatal, "danger" },
            { LogLevel.Info, "#2a80b9" },
        };

        protected override void CloseTarget()
        {
            _client?.Dispose();
            _client = null;

            base.CloseTarget();
        }

    }


}
