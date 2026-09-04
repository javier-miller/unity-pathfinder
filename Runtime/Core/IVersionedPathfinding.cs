using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Optional pathfinding capability for versioned regional updates and validation
    /// of retained routes against the current navigation snapshot.
    /// </summary>
    public interface IVersionedPathfinding
    {
        /// <summary>
        /// Gets the version of the current navigation-grid snapshot.
        /// </summary>
        long GridVersion { get; }

        /// <summary>
        /// Resamples a bounded region without changing the grid geometry.
        /// </summary>
        bool TryRefreshRegion(
            Bounds worldBounds,
            out GridRegionUpdateResult result);

        /// <summary>
        /// Checks whether the retained suffix remains walkable after the grid
        /// advanced from <paramref name="pathGridVersion"/>.
        /// </summary>
        bool IsPathWalkable(
            Vector3 startWorldPosition,
            IReadOnlyList<Vector3> waypoints,
            int firstWaypointIndex,
            long pathGridVersion,
            PathQueryOptions options = null);
    }

}
