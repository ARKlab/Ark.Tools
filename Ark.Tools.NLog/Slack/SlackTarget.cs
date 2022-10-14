using NLog;
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
        [RequiredParameter]
        public string WebHookUrl { get; set; }
        public string AppName { get; set; }

        private SlackClient _client = null;

        public SlackTarget() : base()
        {
            Layout = "${message}";
        }

        public override IList<TargetPropertyWithContext> ContextProperties { get; } = new List<TargetPropertyWithContext>();

        [ArrayParameter(typeof(TargetPropertyWithContext), "field")]
        public IList<TargetPropertyWithContext> Fields => ContextProperties;

        protected override void InitializeTarget()
        {
            if (String.IsNullOrWhiteSpace(this.WebHookUrl))
                throw new ArgumentOutOfRangeException("WebHookUrl", "Webhook URL cannot be empty.");

            Uri uriResult;
            if (!Uri.TryCreate(this.WebHookUrl, UriKind.Absolute, out uriResult))
                throw new ArgumentOutOfRangeException("WebHookUrl", "Webhook URL is an invalid URL.");

            _client = new SlackClient(this.WebHookUrl);

            if (this.ContextProperties.Count == 0)
            {
                this.ContextProperties.Add(new TargetPropertyWithContext("TimestampUtc", "${date:universalTime=true}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("LogLevel", "${level:uppercase=true}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("Host", "${machinename}"));
                this.ContextProperties.Add(new TargetPropertyWithContext("Logger", "${logger}"));
                if (!string.IsNullOrWhiteSpace(AppName))
                    this.ContextProperties.Add(new TargetPropertyWithContext("AppName", AppName));
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
                slack.AddAttachment(exception.Message, color, new[] { ($"Type: {exception.GetType()}", exception.StackTrace ?? "N/A") });
            }

            slack.Send(_client);
        }

        private string _getSlackColorFromLogLevel(LogLevel level)
        {
            if (_logLevelSlackColorMap.TryGetValue(level, out var color))
                return color;
            else
                return "#cccccc";
        }

        private static readonly Dictionary<LogLevel, string> _logLevelSlackColorMap = new Dictionary<LogLevel, string>()
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
