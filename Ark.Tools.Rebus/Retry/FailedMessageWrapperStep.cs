using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Rebus.Bus;
using Rebus.Exceptions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.Simple;

namespace Ark.Tools.Rebus.Retry
{
    [StepDocumentation(@"When 2nd level retries are enabled, a message that has failed too many times must be dispatched as a IFailed<TMessage>.

This is carried out by having the retry step add the '" + ArkRetryStrategyStep.DispatchAsFailedMessageKey + @"' key to the context,
which is then detected by this wrapper step.")]
    class FailedMessageWrapperStep : IIncomingStep
    {
        readonly IErrorTracker _errorTracker;

        public FailedMessageWrapperStep(IErrorTracker errorTracker) => _errorTracker = errorTracker ?? throw new ArgumentNullException(nameof(errorTracker));

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            if (context.Load<bool>(ArkRetryStrategyStep.DispatchAsFailedMessageKey))
            {
                var originalMessage = context.Load<Message>();

                var messageId = originalMessage.GetMessageId();
                var fullErrorDescription = _errorTracker.GetFullErrorDescription(messageId) ?? "(not available in the error tracker!)";
                var exceptions = _errorTracker.GetExceptions(messageId);
                var headers = originalMessage.Headers;
                var body = originalMessage.Body;
                var wrappedBody = _wrapInFailed(headers, body, fullErrorDescription, exceptions);

                context.Save(new Message(headers, wrappedBody));
            }

            await next();
        }

        static readonly ConcurrentDictionary<Type, MethodInfo> _wrapperMethods = new ConcurrentDictionary<Type, MethodInfo>();

        object _wrapInFailed(Dictionary<string, string> headers, object body, string errorDescription, IEnumerable<Exception> exceptions)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (body == null) throw new ArgumentNullException(nameof(body));

            try
            {
                return _wrapperMethods
                    .GetOrAdd(body.GetType(), type =>
                    {
                        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

                        var genericWrapMethod = GetType().GetMethod(nameof(_wrap), bindingFlags);

                        return genericWrapMethod!.MakeGenericMethod(type);
                    })
                    .Invoke(this, new[] { headers, body, errorDescription, exceptions })!;
            }
            catch (Exception exception)
            {
                throw new RebusApplicationException(exception, $"Could not wrap {body} in FailedMessageWrapper<>");
            }
        }

        IFailed<TMessage> _wrap<TMessage>(Dictionary<string, string> headers, TMessage body, string errorDescription, IEnumerable<Exception> exceptions)
        {
            return new FailedMessageWrapper<TMessage>(headers, body, errorDescription, exceptions);
        }
    }
}