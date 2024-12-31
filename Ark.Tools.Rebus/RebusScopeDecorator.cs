using System;
using System.Threading.Tasks;

using Rebus.Handlers;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.Rebus
{
    public class RebusScopeDecorator<T> : IHandleMessages<T>
	{
		private readonly Func<IHandleMessages<T>> _inner;
		private readonly Container _container;

		public RebusScopeDecorator(Func<IHandleMessages<T>> inner, Container container)
		{
			_inner = inner;
			_container = container;
		}

		public async Task Handle(T message)
		{
            await using (AsyncScopedLifestyle.BeginScope(_container).ConfigureAwait(false))
			{
				await _inner().Handle(message).ConfigureAwait(false);
			}
		}
	}
}
