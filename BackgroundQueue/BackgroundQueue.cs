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
		private IBackgroundQueueState _state;

		public BackgroundQueue(BackgroundQueueOptions options)
		{
			_state = new BackgroundQueueState(options);
		}

		public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var state = Interlocked.Exchange(ref _state, null);
			if (state == null)
				throw new InvalidOperationException("Cannot Stop the Queue more than once.");

			using (state)
			{
				await state.StopAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public virtual Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> workItem)
		{
			var state = Interlocked.CompareExchange(ref _state, null, null);
			if (state == null)
				throw new InvalidOperationException("Cannot Enqueue after the Queue has been Stopped.");

			return state.Enqueue(workItem);
		}

	}
}