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
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly TaskCompletionSource<int> _final = new TaskCompletionSource<int>();
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

			var linkedTokenCompletionSource = new TaskCompletionSource<int>();

			using (cancellationTokenSource)
			using (var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout))
			using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
			using (linkedCts.Token.Register(() => linkedTokenCompletionSource.SetCanceled()))
			{
				// wait for all tasks to stop
				await decrementTask.ConfigureAwait(false);
				var task = await Task.WhenAny(_final.Task, linkedTokenCompletionSource.Task).ConfigureAwait(false);
				if (task == linkedTokenCompletionSource.Task)
					linkedCts.Token.ThrowIfCancellationRequested();
			}
		}

		/// <inheritdoc />
		public virtual Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem)
		{
			var cancellationTokenSource = Interlocked.CompareExchange(ref _cancellationTokenSource, null, null);
			if (cancellationTokenSource == null)
				throw new ObjectDisposedException(GetType().FullName);

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
						_final.TrySetResult(0);
					}
				}

				// this outer task must not be cancelled
			}, CancellationToken.None);

			return task;
		}

	}
}