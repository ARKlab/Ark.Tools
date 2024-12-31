// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NLog;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ProfileCommandDecorator<TCommand> : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        // We use Logger to trace the profile results. Could be written to a Db but I'm lazy atm.
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly ICommandHandler<TCommand> _decorated;

        public ProfileCommandDecorator(ICommandHandler<TCommand> decorated)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));

            _decorated = decorated;
        }

        public void Execute(TCommand command)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();
            _decorated.Execute(command);
            stopWatch.Stop();
            _logger.Trace(CultureInfo.InvariantCulture, "Command<{Command}> executed in {Elapsed}ms", command.GetType(), stopWatch.ElapsedMilliseconds);
        }

        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();
            await _decorated.ExecuteAsync(command, ctk).ConfigureAwait(false);
            stopWatch.Stop();
            _logger.Trace(CultureInfo.InvariantCulture, "Command<{Command}> executed in {Elapsed}ms", command.GetType(), stopWatch.ElapsedMilliseconds);
        }
    }
}
