using System;
using System.Threading.Tasks;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Represents one path request accepted by a scheduler.
    /// </summary>
    public sealed class PathRequestHandle
    {
        private readonly TaskCompletionSource<PathResult> _completionSource =
            new TaskCompletionSource<PathResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<PathRequestHandle, bool> _cancel;

        internal PathRequestHandle(
            long requestId,
            PathRequestPriority priority,
            Func<PathRequestHandle, bool> cancel)
        {
            RequestId = requestId;
            Priority = priority;
            Status = PathRequestStatus.Queued;
            _cancel = cancel;
        }

        /// <summary>
        /// Gets the scheduler-local identifier of this request.
        /// </summary>
        public long RequestId { get; }

        public PathRequestPriority Priority { get; }

        public PathRequestStatus Status { get; private set; }

        /// <summary>
        /// Gets scheduler timings and diagnostics after completion.
        /// </summary>
        public PathRequestMetrics Metrics { get; private set; }

        public bool IsCompleted =>
            Status == PathRequestStatus.Completed ||
            Status == PathRequestStatus.Cancelled;

        /// <summary>
        /// Gets the terminal result, or null while the request is queued or running.
        /// </summary>
        public PathResult Result { get; private set; }

        /// <summary>
        /// Completes when the query finishes or is cancelled before execution.
        /// </summary>
        public Task<PathResult> Completion => _completionSource.Task;

        /// <summary>
        /// Cancels the request while it is still queued.
        /// A query already running synchronously cannot be interrupted.
        /// </summary>
        public bool Cancel() => _cancel != null && _cancel(this);

        internal void MarkRunning()
        {
            if (Status == PathRequestStatus.Queued)
            {
                Status = PathRequestStatus.Running;
            }
        }

        internal void Complete(
            PathResult result,
            bool cancelled,
            PathRequestMetrics metrics)
        {
            if (IsCompleted)
            {
                return;
            }

            Result = result;
            Metrics = metrics;
            Status = cancelled
                ? PathRequestStatus.Cancelled
                : PathRequestStatus.Completed;
            _cancel = null;
            _completionSource.TrySetResult(result);
        }
    }
}
