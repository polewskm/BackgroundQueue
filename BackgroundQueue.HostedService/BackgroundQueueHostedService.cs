using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackgroundQueue.HostedService
{
	internal interface IBackgroundQueueHostedService : IBackgroundQueue, IHostedService
	{
		// nothing
	}

	public class BackgroundQueueHostedService : BackgroundQueueService, IBackgroundQueueHostedService
	{
		private readonly ILogger<BackgroundQueueHostedService> _logger;

		public BackgroundQueueHostedService(ILogger<BackgroundQueueHostedService> logger, IOptions<BackgroundQueueOptions> options)
			: base(options?.Value ?? throw new ArgumentNullException(nameof(options)))
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		protected override void OnStarting()
		{
			_logger.LogInformation("BackgroundQueueHostedService.OnStarting");

			base.OnStarting();
		}

		protected override void OnStopping(int activeCount)
		{
			_logger.LogInformation("BackgroundQueueHostedService.OnStopping: ActiveCount={0}", activeCount);
		}

		protected override void OnStopped(int activeCount, bool graceful)
		{
			_logger.LogInformation("BackgroundQueueHostedService.OnStopped: ActiveCount={0}; Graceful={1}", activeCount, graceful);
		}

		protected override void OnEnqueue(int activeCount)
		{
			_logger.LogInformation("BackgroundQueueHostedService.OnEnqueue: ActiveCount={0}", activeCount);
		}

	}
}