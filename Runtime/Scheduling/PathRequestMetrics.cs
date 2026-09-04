namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Immutable timing snapshot for one scheduled path request.
    /// Times are measured on the main thread and exclude callback execution.
    /// </summary>
    public readonly struct PathRequestMetrics
    {
        internal PathRequestMetrics(
            double queueMilliseconds,
            double executionMilliseconds,
            double pathfindingMilliseconds,
            int enqueuedFrame,
            int completedFrame,
            bool wasCacheHit,
            bool priorityWasAged)
        {
            QueueMilliseconds = queueMilliseconds;
            ExecutionMilliseconds = executionMilliseconds;
            PathfindingMilliseconds = pathfindingMilliseconds;
            EnqueuedFrame = enqueuedFrame;
            CompletedFrame = completedFrame;
            WasCacheHit = wasCacheHit;
            PriorityWasAged = priorityWasAged;
        }

        public double QueueMilliseconds { get; }

        public double ExecutionMilliseconds { get; }

        /// <summary>
        /// Gets the time spent inside IPathfinding.FindPath. It is zero for cache
        /// hits and excludes cache bookkeeping and scheduler completion work.
        /// </summary>
        public double PathfindingMilliseconds { get; }

        public double SchedulingOverheadMilliseconds =>
            System.Math.Max(0d, ExecutionMilliseconds - PathfindingMilliseconds);

        public double TotalMilliseconds =>
            QueueMilliseconds + ExecutionMilliseconds;

        public int EnqueuedFrame { get; }

        public int CompletedFrame { get; }

        public bool WasCacheHit { get; }

        public bool PriorityWasAged { get; }
    }
}
