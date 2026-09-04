using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Resolves navigation positions and detailed paths in world space.
    /// </summary>
    public interface IPathfinding
    {
        /// <summary>
        /// Tries to resolve a walkable cell center from a world position.
        /// </summary>
        bool TryGetWalkablePosition(Vector3 position, out Vector3 result);

        /// <summary>
        /// Finds a path using explicit query options and returns a detailed result.
        /// </summary>
        PathResult FindPath(
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            PathQueryOptions options);
    }
}
