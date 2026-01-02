using Slack.Webhooks;

using System;
using System.Collections.Generic;

namespace Ark.Tools.NLog.Slack
{
    public class SlackMessageBuilder
    {
        private readonly SlackMessage _slackMessage;
#pragma warning disable MA0046 // Use EventHandler<T> to declare events
        private event Action<Exception>? Error;
#pragma warning restore MA0046 // Use EventHandler<T> to declare events
        private bool _existError;

        public SlackMessageBuilder()
        {
            _slackMessage = new SlackMessage();
        }

        public static SlackMessageBuilder Build()
        {
            return new SlackMessageBuilder();
        }

        public SlackMessageBuilder WithMessage(string message)
        {
            this._slackMessage.Text = message;

            return this;
        }

        public SlackMessageBuilder AddAttachment(string color, IEnumerable<(string title, string? text)> fields)
        {
            var a = new SlackAttachment
            {
                Color = color
            };

            a.Fields = new List<SlackField>();

            foreach (var (title, text) in fields)
                a.Fields.Add(new SlackField()
                {
                    Title = title,
                    Value = text,
                    Short = true
                });

            if (this._slackMessage.Attachments == null)
                this._slackMessage.Attachments = new List<SlackAttachment>();

            this._slackMessage.Attachments.Add(a);

            return this;
        }

        public SlackMessageBuilder AddAttachment(string message, string color, IEnumerable<(string title, string text)> fields)
        {
            var a = new SlackAttachment
            {
                Text = message,
                Color = color
            };

            a.Fields = new List<SlackField>();

            foreach (var (title, text) in fields)
                a.Fields.Add(new SlackField()
                {
                    Title = title,
                    Value = text,
                    Short = true
                });

            if (this._slackMessage.Attachments == null)
                this._slackMessage.Attachments = new List<SlackAttachment>();

            this._slackMessage.Attachments.Add(a);

            return this;
        }

        public SlackMessageBuilder OnError(Action<Exception> error)
        {
            this.Error += error;
            _existError = true;

            return this;
        }

        public void Send(SlackClient client)
        {
            try
            {
                client.Post(_slackMessage);
            }
            catch (Exception e)
            {
                if (_existError)
                    Error?.Invoke(e);
                else
                    throw;
            }
        }
    }
}