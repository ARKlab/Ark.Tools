// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ExceptionLogCommandDecorator<TCommand> : ICommandHandler<TCommand>
    {
        private readonly ICommandHandler<TCommand> _decorated;

        public ExceptionLogCommandDecorator(ICommandHandler<TCommand> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
        }

        public void Execute(TCommand command)
        {
            try
            {
                _decorated.Execute(command);
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
                logger.Error(ex, "Exception occured");
                throw;
            }
        }

        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken))
        {
            try
            {
                await _decorated.ExecuteAsync(command, ctk).ConfigureAwait(false);
            } catch (Exception ex)
            {
                Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
                logger.Error(ex, "Exception occured");
                throw;
            }
        }
    }
}
