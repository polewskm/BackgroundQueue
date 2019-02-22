using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BackgroundQueue.HostedService
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddBackgroundQueue(this IServiceCollection services)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));

			services.AddSingleton<IBackgroundQueueHostedService, BackgroundQueueHostedService>();
			services.AddSingleton<IBackgroundQueue>(sp => sp.GetRequiredService<IBackgroundQueueHostedService>());
			services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<IBackgroundQueueHostedService>());

			return services;
		}

	}
}