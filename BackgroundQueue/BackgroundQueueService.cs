﻿using System;
using System.Threading;
using System.Threading.Tasks;
using BackgroundQueue.States;

namespace BackgroundQueue
{
	public interface IBackgroundQueueService : IBackgroundQueue, IDisposable
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

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			var state = Interlocked.Exchange(ref _state, null);
			state?.Dispose();
		}

		protected virtual void OnStarting()
		{
			// nothing
		}

		protected virtual void OnStopping(int activeCount)
		{
			// nothing
		}

		protected virtual void OnStopped(int activeCount, bool graceful)
		{
			// nothing
		}

		protected virtual void OnEnqueue(int activeCount)
		{
			// nothing
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

			try
			{
				OnStarting();
			}
			catch
			{
				// ignore
			}

			return state.StartAsync(cancellationToken);
		}

		/// <inheritdoc />
		public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var state = Interlocked.Exchange(ref _state, StoppedState) ?? StoppedState;

			try
			{
				OnStopping(state.ActiveCount);
			}
			catch
			{
				// ignore
			}

			var graceful = false;
			try
			{
				await state.StopAsync(cancellationToken).ConfigureAwait(false);
				graceful = true;
			}
			finally
			{
				state.Dispose();

				try
				{
					OnStopped(state.ActiveCount, graceful);
				}
				catch
				{
					// ignore
				}
			}
		}

		/// <inheritdoc />
		public virtual Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback)
		{
			var state = Interlocked.CompareExchange(ref _state, null, null) ?? StoppedState;

			try
			{
				OnEnqueue(state.ActiveCount);
			}
			catch
			{
				// ignore
			}

			return state.Enqueue(callback);
		}

	}
}