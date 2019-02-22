﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace BackgroundQueue.NetFx
{
	public class BackgroundQueueRegisteredObject : IRegisteredObject
	{
		private CancellationTokenSource _cancellationTokenSource;
		private IBackgroundQueueService _service;
		private Task _stopTask;

		public IBackgroundQueue BackgroundQueue => _service;

		public BackgroundQueueRegisteredObject()
		{
			HostingEnvironment.RegisterObject(this);
		}

		public void Stop(bool immediate)
		{
			if (immediate)
			{
				var stopTask = Interlocked.Exchange(ref _stopTask, null);
				var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, null);
				var cancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;
				try
				{
					if (stopTask == null)
					{
						stopTask = _service.StopAsync(cancellationToken);
					}
					else
					{
						try
						{
							_cancellationTokenSource?.Cancel();
						}
						catch
						{
							// ignore unhandled exceptions from registered callbacks
						}
					}

					stopTask.ConfigureAwait(false).GetAwaiter().GetResult();
				}
				finally
				{
					cancellationTokenSource?.Dispose();

					HostingEnvironment.UnregisterObject(this);
				}
			}
			else
			{
				var cancellationTokenSource = new CancellationTokenSource();
				var prevCancellationTokenSource = Interlocked.CompareExchange(ref _cancellationTokenSource, cancellationTokenSource, null);
				if (prevCancellationTokenSource != null)
				{
					cancellationTokenSource.Dispose();
					cancellationTokenSource = prevCancellationTokenSource;
				}

				var stopTask = _service.StopAsync(cancellationTokenSource.Token);
				Interlocked.CompareExchange(ref _stopTask, stopTask, null);
			}
		}

	}

	public static class HttpContextExtensions
	{
		private static readonly object Slot = new object();

		public static IBackgroundQueue GetBackgroundQueue(this HttpContext httpContext)
		{
			var item = httpContext.Items[Slot] as BackgroundQueueRegisteredObject;
			if (item == null)
			{
				lock (Slot)
				{
					item = httpContext.Items[Slot] as BackgroundQueueRegisteredObject;
					if (item == null)
					{
						item = new BackgroundQueueRegisteredObject();
						httpContext.Items[Slot] = item;
					}
				}
			}

			return item.BackgroundQueue;
		}

	}
}