using System.ComponentModel;
using System.Threading.Tasks;

namespace BackgroundQueue
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public interface IWorkItemBase
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		Task BaseTask { get; }
	}

	public interface IWorkItem : IWorkItemBase
	{
		Task Task { get; }
	}

	public interface IWorkItem<TResult> : IWorkItemBase
	{
		Task<TResult> Task { get; }
	}

	public class WorkItem : IWorkItem
	{
		Task IWorkItemBase.BaseTask => Task;

		public Task Task { get; set; }
	}

	public class WorkItem<TResult> : IWorkItem<TResult>
	{
		Task IWorkItemBase.BaseTask => Task;

		public Task<TResult> Task { get; set; }
	}
}