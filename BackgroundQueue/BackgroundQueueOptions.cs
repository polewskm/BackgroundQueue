using System;

namespace BackgroundQueue
{
	public class BackgroundQueueOptions
	{
		/// <summary>
		/// The default timeout for <see cref="IBackgroundQueue.StopAsync"/>.
		/// </summary>
		public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
	}
}