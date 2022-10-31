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
				_logger.Debug("Processed message type {Type} in {Elapsed}ms", typeof(T).FullName, sw.ElapsedMilliseconds);
			}
			catch (Exception e)
			{
				_logger.Warn(e, "Failed processing message type {Type} in {Elapsed}ms", typeof(T).FullName, sw.ElapsedMilliseconds);
				throw;
			}
		}
	}
}
