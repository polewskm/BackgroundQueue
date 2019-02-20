using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue.States
{
	public interface IBackgroundQueueState : IDisposable
	{
		Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback);

		//void OnEnqueue();
		//Task OnStarting(CancellationToken cancellationToken);
		//Task OnCompleted(Task antecedent);
	}
}