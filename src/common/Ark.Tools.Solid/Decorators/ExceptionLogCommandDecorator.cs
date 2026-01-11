// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 



namespace Ark.Tools.Solid.Decorators;

public sealed class ExceptionLogCommandDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _decorated;

    public ExceptionLogCommandDecorator(ICommandHandler<TCommand> decorated)
    {
        ArgumentNullException.ThrowIfNull(decorated);

        _decorated = decorated;
    }

    public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default)
    {
        try
        {
            await _decorated.ExecuteAsync(command, ctk).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger logger = LogManager.GetLogger(_decorated.GetType().ToString());
            logger.Error(ex, "Exception occured");
            throw;
        }
    }
}