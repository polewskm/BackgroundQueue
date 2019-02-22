using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue.States
{
	internal interface IBackgroundQueueState : IDisposable
	{
		int ActiveCount { get; }

		Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback);
	}
}