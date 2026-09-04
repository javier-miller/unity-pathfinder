using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Optional capability implemented by grid sources that can resample a bounded
    /// part of an existing grid without rebuilding its geometry.
    /// </summary>
    public interface IIncrementalPathfindingGridSource
    {
        bool TryRefreshRegion(
            Grid grid,
            Bounds worldBounds,
            out GridRegionUpdateResult result,
            out string errorMessage);
    }
}
