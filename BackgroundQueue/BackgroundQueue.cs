using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public interface IBackgroundQueue
	{
		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem);
	}

	public class BackgroundQueue : IBackgroundQueue
	{
		private readonly BackgroundQueueOptions _options;
		private readonly TaskCompletionSource<int> _final = new TaskCompletionSource<int>();
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		// when this count reaches zero, then the queue is considered closed and waiting for shutdown
		private int _active = 1;

		public BackgroundQueue(BackgroundQueueOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
		}

		public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, null);
			if (cancellationTokenSource == null) return;

			var decrementTask = Enqueue(token =>
			{
				var count = Interlocked.Decrement(ref _active);
				return Task.FromResult(count);

				// this task must not be cancelled
			}, CancellationToken.None);

			// signal that active tasks should stop
			try
			{
				cancellationTokenSource.Cancel(false);
			}
			catch
			{
				// ignore unhandled exceptions from registered callbacks
			}

			using (cancellationTokenSource)
			using (var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout))
			using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
			using (linkedCts.Token.Register(() => _final.TrySetCanceled()))
			{
				// wait for all tasks to stop
				await decrementTask.ConfigureAwait(false);
				await _final.Task.ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public virtual Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem)
		{
			var cancellationTokenSource = Interlocked.CompareExchange(ref _cancellationTokenSource, null, null);
			if (cancellationTokenSource == null)
				throw new InvalidOperationException("Cannot Enqueue after Queue has been Stopped.");

			return Enqueue(workItem, cancellationTokenSource.Token);
		}

		private Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _active);

			var task = Task.Run(async () =>
			{
				try
				{
					cancellationToken.ThrowIfCancellationRequested();
					return await workItem(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					var count = Interlocked.Decrement(ref _active);
					if (count == 0)
					{
						// signal that all tasks are complete
						// and that we are shutting down
						_final.TrySetResult(0);
					}
				}

				// this outer task must not be cancelled
			}, CancellationToken.None);

			return task;
		}

	}
}