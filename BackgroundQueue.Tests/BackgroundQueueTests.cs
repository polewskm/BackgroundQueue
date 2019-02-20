using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BackgroundQueue.Tests
{
	public class BackgroundQueueTests
	{
		[Fact]
		public async Task Enqueue_GivenResult_ThenIsValid()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			var result = await queue.Enqueue(token => Task.FromResult(1)).ConfigureAwait(false);
			Assert.Equal(1, result);

			// cleanup
			await queue.StopAsync().ConfigureAwait(false);
		}

		[Fact]
		public async Task Enqueue_GivenDelay_ThenIsValid()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			var delay = TimeSpan.FromMilliseconds(100);
			var stopwatch = Stopwatch.StartNew();
			var elapsed = await queue.Enqueue(async token =>
			{
				await Task.Delay(delay, token).ConfigureAwait(false);
				return stopwatch.Elapsed;
			}).ConfigureAwait(false);

			var delta = elapsed - delay;
			Assert.InRange(delta.TotalMilliseconds, 0, 25);

			// cleanup
			await queue.StopAsync().ConfigureAwait(false);
		}

		[Fact]
		public async Task Enqueue_GivenException_ThenException()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			await Assert.ThrowsAsync<ApplicationException>(async () =>
				await queue.Enqueue<int>(token => throw new ApplicationException()).ConfigureAwait(false)
			).ConfigureAwait(false);

			// cleanup
			await queue.StopAsync().ConfigureAwait(false);
		}

		[Fact]
		public async Task Enqueue_GivenStop_ThenDisposed()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			await queue.StopAsync().ConfigureAwait(false);

			await Assert.ThrowsAsync<InvalidOperationException>(async () =>
					await queue.Enqueue(token => Task.FromResult(1)).ConfigureAwait(false)
			).ConfigureAwait(false);
		}

		[Fact]
		public async Task StopAsync_GivenNoTasks_ThenStopsImmediately()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			var stopwatch = Stopwatch.StartNew();
			await queue.StopAsync().ConfigureAwait(false);
			var elapsed = stopwatch.Elapsed;

			Assert.InRange(elapsed.TotalMilliseconds, 0, 25);
		}

		[Fact]
		public async Task StopAsync_GivenTask_ThenTaskCanceled()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			var tcs = new TaskCompletionSource<int>();

			var delay = TimeSpan.FromMilliseconds(100);
			var stopwatch = Stopwatch.StartNew();
			var delayTask = queue.Enqueue(async token =>
			{
				tcs.SetResult(0);

				await Task.Delay(delay, token).ConfigureAwait(false);
				return stopwatch.Elapsed;
			});

			// make sure the delay task has started
			await tcs.Task.ConfigureAwait(false);

			var stopTask = queue.StopAsync();
			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
				await Task.WhenAll(delayTask, stopTask).ConfigureAwait(false)
			).ConfigureAwait(false);

			// the stop task should have ran to completion
			await stopTask.ConfigureAwait(false);
		}

		[Fact]
		public async Task StopAsync_GivenCancelledToken_ThenTaskCanceled()
		{
			var options = new BackgroundQueueOptions();
			var queue = new BackgroundQueueService(options);

			var tcs = new TaskCompletionSource<int>();
			var delay = queue.Enqueue(token => tcs.Task);

			var cancellationToken = new CancellationToken(true);
			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
				await queue.StopAsync(cancellationToken).ConfigureAwait(false)
			).ConfigureAwait(false);

			tcs.SetResult(0);
			await delay.ConfigureAwait(false);
		}

		[Fact]
		public async Task StopAsync_GivenShutdownTimeout_ThenTaskCanceled()
		{
			var options = new BackgroundQueueOptions
			{
				ShutdownTimeout = TimeSpan.FromMilliseconds(50)
			};

			var queue = new BackgroundQueueService(options);

			var tcs = new TaskCompletionSource<int>();
			var delay = queue.Enqueue(token => tcs.Task);

			var stopwatch = Stopwatch.StartNew();
			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
				await queue.StopAsync(CancellationToken.None).ConfigureAwait(false)
			).ConfigureAwait(false);
			var elapsed = stopwatch.Elapsed;

			Assert.InRange(elapsed.TotalMilliseconds, 50, 100);

			tcs.SetResult(0);
			await delay.ConfigureAwait(false);
		}

	}
}