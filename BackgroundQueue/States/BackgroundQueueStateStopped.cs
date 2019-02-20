using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue.States
{
	public class BackgroundQueueStateStopped : IBackgroundQueueState
	{
		public void Dispose()
		{
			// nothing
		}

		public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			throw new NotImplementedException();
		}

		public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.CompletedTask;
		}

		public Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback)
		{
			throw new InvalidOperationException("Cannot enqueue when service is stopped.");
		}

	}
}