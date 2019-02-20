using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public interface IWorkItemBase
	{
		Task GetTask();
	}

	public interface IWorkItem : IWorkItemBase
	{
		Task Task { get; }
	}

	public interface IWorkItem<TResult> : IWorkItemBase
	{
		Task<TResult> Task { get; }
	}

	public class WorkItem : IWorkItem
	{
		public Task GetTask() => Task;

		public Task Task { get; set; }
	}

	public class WorkItem<TResult> : IWorkItem<TResult>
	{
		public Task GetTask() => Task;

		public Task<TResult> Task { get; set; }
	}

	public interface IQueue2
	{
		IWorkItem QueueBackgroundWorkItem<TResult>(Func<CancellationToken, Task> workItemCallback);

		IWorkItem<TResult> QueueBackgroundWorkItem<TResult>(Func<CancellationToken, Task<TResult>> workItemCallback);

		IWorkItemBase DequeueAsync(CancellationToken cancellationToken = default(CancellationToken));
	}

	public interface IBackgroundQueueState : IDisposable
	{
		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem);

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

		private int _stop;
		private int _total;

		// when this count reaches zero, then the queue is considered closed and waiting for shutdown
		private int _active = 1;

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

		public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (Interlocked.Exchange(ref _stop, 1) != 0) return;

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

		public Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem)
		{
			var cancellationToken = _cancellationTokenSource.Token;
			return Enqueue(workItem, cancellationToken);
		}

		public Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem, CancellationToken cancellationToken)
		{
			OnEnqueue();

			// the scheduler is only used to enforce concurrency for our queue
			const TaskCreationOptions creationOptions =
				TaskCreationOptions.HideScheduler |
				TaskCreationOptions.DenyChildAttach;

			// dispatch the work item onto the task scheduler
			var task = Task.Factory.StartNew(async () =>
			{
				await OnStarting(cancellationToken).ConfigureAwait(false);

				var result = await workItem(cancellationToken).ConfigureAwait(false);
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