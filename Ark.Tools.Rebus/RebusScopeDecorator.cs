using System;
using System.Threading.Tasks;
using NLog;
using Rebus.Handlers;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.Rebus
{
	public class RebusScopeDecorator<T> : IHandleMessages<T>
	{
		private readonly Func<IHandleMessages<T>> _inner;
		private readonly Container _container;
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		public RebusScopeDecorator(Func<IHandleMessages<T>> inner, Container container)
		{
			_inner = inner;
			_container = container;
		}

		public async Task Handle(T message)
		{
			using (AsyncScopedLifestyle.BeginScope(_container))
			{
				await _inner().Handle(message);
			}
		}
	}
}
