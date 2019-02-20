using System;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	public interface IBackgroundQueue
	{
		Task<TResult> Enqueue<TResult>(Func<CancellationToken, Task<TResult>> callback);
	}
}