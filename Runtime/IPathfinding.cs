using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Interface
    /// </summary>
    public interface IPathfinding
    {
        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        bool GetPath(Vector3 position, out Vector3 result);

        /// <summary>
        /// Finds the path.
        /// </summary>
        /// <param name="startWorldPosition">The start world position.</param>
        /// <param name="endWorldPosition">The end world position.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        bool FindPath(Vector3 startWorldPosition, Vector3 endWorldPosition, out IEnumerable<Vector3> path);
    }
}
