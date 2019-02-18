using System;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public class BackgroundQueueOptions
	{
		/// <summary>
		/// Gets or sets the <see cref="TaskScheduler"/> that is used to schedule
		/// work items onto the thread-pool. If not specified, the default value
		/// is <see cref="TaskScheduler.Default"/>. 
		/// </summary>
		public TaskScheduler Scheduler { get; set; }

		/// <summary>
		/// The default timeout for <see cref="IBackgroundQueue.StopAsync"/>.
		/// </summary>
		public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
	}
}