using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;
using Rebus.Handlers;

namespace Ark.Tools.Rebus
{
	public class RebusLogDecorator<T> : IHandleMessages<T>
	{
		private readonly IHandleMessages<T> _inner;
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		public RebusLogDecorator(IHandleMessages<T> inner)
		{
			_inner = inner;
		}

		public async Task Handle(T message)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				await _inner.Handle(message);
				_logger.Debug($"Processed message type {typeof(T).Name} in {sw.ElapsedMilliseconds}ms");
			}
			catch (Exception e)
			{
				_logger.Warn(e, $"Failed processing message type {typeof(T).Name} after {sw.ElapsedMilliseconds}ms");
				throw;
			}
		}
	}
}
