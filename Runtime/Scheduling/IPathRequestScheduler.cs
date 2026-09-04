using System;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Queues path queries and processes them according to a per-frame budget.
    /// Its members must be called from Unity's main thread.
    /// </summary>
    public interface IPathRequestScheduler
    {
        int PendingCount { get; }

        bool IsProcessing { get; }

        int LastFrameProcessedCount { get; }

        double LastFrameElapsedMilliseconds { get; }

        PathRequestHandle Enqueue(
            IPathfinding pathfinder,
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            PathQueryOptions options = null,
            PathRequestPriority priority = PathRequestPriority.Normal,
            Action<PathRequestHandle, PathResult> completed = null);

        bool Cancel(PathRequestHandle request);

        int CancelAll();
    }
}
