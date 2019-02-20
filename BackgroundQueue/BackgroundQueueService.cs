using System;
using System.Threading;
using System.Threading.Tasks;
using BackgroundQueue.States;

namespace BackgroundQueue
{
	public interface IBackgroundQueueService : IBackgroundQueue
	{
		Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

		Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));
	}

	public class BackgroundQueueService : IBackgroundQueueService
	{
		private readonly BackgroundQueueOptions _options;
		private static readonly IBackgroundQueueState StoppedState = new BackgroundQueueStateStopped();
		private IBackgroundQueueState _state;

		public BackgroundQueueService(BackgroundQueueOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
		}

		/// <inheritdoc />
		public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var state = Interlocked.CompareExchange(ref _state, null, null);
			if (state == null)
			{
				var newState = new BackgroundQueueStateStarted(_options);
				state = Interlocked.CompareExchange(ref _state, newState, null);
				if (state != null)
				{
					newState.Dispose();
				}
				else
				{
					state = newState;
				}
			}

			return state.StartAsync(cancellationToken);
		}

		/// <inheritdoc />
		public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var state = Interlocked.Exchange(ref _state, StoppedState) ?? StoppedState;

			using (state)
			{
				return state.StopAsync(cancellationToken);
			}
		}

		/// <inheritdoc />
		public virtual Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback)
		{
			var state = Interlocked.CompareExchange(ref _state, null, null) ?? StoppedState;

			return state.Enqueue(callback);
		}

	}
}