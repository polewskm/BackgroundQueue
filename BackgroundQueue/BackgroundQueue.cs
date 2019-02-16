using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public interface IBackgroundQueue : IDisposable
	{
		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem);
	}

	public class BackgroundQueue : IBackgroundQueue
	{
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly TaskCompletionSource<int> _final = new TaskCompletionSource<int>();
		private int _active = 1;

		/// <inheritdoc />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
		}

		private async Task StopAsync(CancellationToken cancellationToken)
		{
			var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, null);
			if (cancellationTokenSource == null) return;

			var decrementTask = Enqueue(token =>
			{
				var count = Interlocked.Decrement(ref _active);
				return Task.FromResult(count);
			}, CancellationToken.None).ConfigureAwait(false);

			// signal that active tasks should stop
			try
			{
				cancellationTokenSource.Cancel(false);
			}
			catch
			{
				// ignore unhandled exceptions from registered callbacks
			}

			// wait for all tasks to stop
			var canceledCompletionSource = new TaskCompletionSource<int>();
			using (cancellationTokenSource)
			using (cancellationToken.Register(() => canceledCompletionSource.SetCanceled()))
			{
				var task = await Task.WhenAny(_final.Task, canceledCompletionSource.Task).ConfigureAwait(false);
				if (task == canceledCompletionSource.Task)
				{
					return Task.FromCanceled(cancellationToken);
				}
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
			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled<TResult>(cancellationToken);

			Interlocked.Increment(ref _active);

			var task = Task.Run(async () =>
			{
				try
				{
					return await workItem(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					var count = Interlocked.Decrement(ref _active);
					if (count == 0)
						_final.TrySetResult(0);
				}
			}, CancellationToken.None);

			return task;
		}

	}
}