using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public interface IQueue2
	{
		IWorkItem QueueBackgroundWorkItem(Func<CancellationToken, Task> callback);

		IWorkItem<TResult> QueueBackgroundWorkItem<TResult>(Func<CancellationToken, Task<TResult>> callback);

		Task<IWorkItemBase> DequeueAsync(CancellationToken cancellationToken = default(CancellationToken));
	}

	public interface IBackgroundQueueState : IDisposable
	{
		Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback);

		void OnEnqueue();

		Task OnStarting(CancellationToken cancellationToken);

		Task OnCompleted(Task antecedent);
	}

	public class BackgroundQueueState : IBackgroundQueueState
	{
		private readonly TaskScheduler _scheduler;
		private readonly BackgroundQueueOptions _options;
		private readonly TaskCompletionSource<int> _completion = new TaskCompletionSource<int>();
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		// when this count reaches zero, then the queue is considered closed and waiting for shutdown
		private int _active = 1;
		private int _total;

		private int _state = StateInitial;
		private const int StateInitial = 0;
		private const int StateStarted = 1;
		private const int StateStopped = 2;

		public BackgroundQueueState(BackgroundQueueOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_scheduler = options.Scheduler ?? TaskScheduler.Default;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			_cancellationTokenSource.Dispose();
		}

		public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (Interlocked.CompareExchange(ref _state, StateStarted, StateInitial) != StateInitial)
				throw new InvalidOperationException("TODO");

			return Task.CompletedTask;
		}

		public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (Interlocked.CompareExchange(ref _state, StateStopped, StateStarted) != StateStarted)
				throw new InvalidOperationException("TODO");

			// instruct the queue that we are shutting down by decrementing the
			// active count which will cause the last running task to cleanup
			// our resources
			var decrementTask = Enqueue(token =>
			{
				var count = Interlocked.Decrement(ref _active);
				return Task.FromResult(count);

				// this task must not be cancelled
			}, CancellationToken.None);

			// signal that active tasks should stop
			try
			{
				_cancellationTokenSource.Cancel(false);
			}
			catch
			{
				// ignore unhandled exceptions from registered callbacks
			}

			//ThreadPool.

			using (var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout))
			using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
			using (linkedCts.Token.Register(() => _completion.TrySetCanceled()))
			{
				// wait for all tasks to stop
				await decrementTask.ConfigureAwait(false);
				await _completion.Task.ConfigureAwait(false);
			}
		}

		public Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback)
		{
			var cancellationToken = _cancellationTokenSource.Token;
			return Enqueue(callback, cancellationToken);
		}

		public Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback, CancellationToken cancellationToken)
		{
			if (Interlocked.CompareExchange(ref _state, StateStarted, StateStarted) != StateStarted)
				throw new InvalidOperationException("TODO");

			OnEnqueue();

			// the scheduler is only used to enforce concurrency for our queue
			const TaskCreationOptions creationOptions =
				TaskCreationOptions.HideScheduler |
				TaskCreationOptions.DenyChildAttach;

			// dispatch the work item onto the task scheduler
			var task = Task.Factory.StartNew(async () =>
			{
				await OnStarting(cancellationToken).ConfigureAwait(false);

				var result = await callback(cancellationToken).ConfigureAwait(false);

				return result;

			}, cancellationToken, creationOptions, _scheduler).Unwrap();

			// cleanup when the work item completes
			task.ContinueWith(async antecedent =>
			{
				await OnCompleted(antecedent).ConfigureAwait(false);

				// this continuation cannot be cancelled
				// because OnCompleted must be called
			}, CancellationToken.None);

			return task;
		}

		public void OnEnqueue()
		{
			Interlocked.Increment(ref _active);
			Interlocked.Increment(ref _total);
		}

		public virtual Task OnStarting(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			return Task.CompletedTask;
		}

		public virtual Task OnCompleted(Task antecedent)
		{
			if (Interlocked.Decrement(ref _active) == 0)
			{
				// signal that all tasks are complete
				// and that we have completed shutting down
				_completion.TrySetResult(0);
			}

			return Task.CompletedTask;
		}

	}
}