using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public static class BackgroundQueueExtensions
	{
		private static bool IsCanceled(Exception exception, CancellationToken token)
		{
			if (!token.IsCancellationRequested)
				return false;

			// the following checks for AggregateException and TargetInvocationException
			var operationCanceledException = exception.GetBaseException() as OperationCanceledException;
			return operationCanceledException?.CancellationToken == token;
		}

		private static Task<TResult> FromCanceledOrException<TResult>(Exception exception, CancellationToken token)
		{
			return IsCanceled(exception, token)
				? Task.FromCanceled<TResult>(token)
				: Task.FromException<TResult>(exception);
		}

		public static Task Enqueue(this IBackgroundQueue queue, Action callback)
		{
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			return queue.Enqueue(token =>
			{
				try
				{
					callback();
				}
				catch (Exception exception)
				{
					return FromCanceledOrException<int>(exception, token);
				}

				return Task.FromResult(0);
			});
		}

		public static Task Enqueue(this IBackgroundQueue queue, Action<CancellationToken> callback)
		{
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			return queue.Enqueue(token =>
			{
				try
				{
					callback(token);
				}
				catch (Exception exception)
				{
					return FromCanceledOrException<int>(exception, token);
				}

				return Task.FromResult(0);
			});
		}

		public static Task Enqueue(this IBackgroundQueue queue, Func<CancellationToken, Task> callback)
		{
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			return queue.Enqueue(async token =>
			{
				await callback(token).ConfigureAwait(false);
				return Task.FromResult(0);
			});
		}

		public static Task<TResult> Enqueue<TResult>(this IBackgroundQueue queue, Func<TResult> callback)
		{
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			return queue.Enqueue(token =>
			{
				try
				{
					var result = callback();
					return Task.FromResult(result);
				}
				catch (Exception exception)
				{
					return FromCanceledOrException<TResult>(exception, token);
				}
			});
		}

		public static Task<TResult> Enqueue<TResult>(this IBackgroundQueue queue, Func<CancellationToken, TResult> callback)
		{
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			return queue.Enqueue(token =>
			{
				try
				{
					var result = callback(token);
					return Task.FromResult(result);
				}
				catch (Exception exception)
				{
					return FromCanceledOrException<TResult>(exception, token);
				}
			});
		}

	}
}