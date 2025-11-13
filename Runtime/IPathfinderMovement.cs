using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinder Movement Interface
    /// </summary>
    public interface IPathfinderMovement
    {
        /// <summary>
        /// Moves to.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="moveFinishedCallback">The move finished callback.</param>
        /// <returns></returns>
        bool MoveTo(Vector3 targetPosition, Action<bool> moveFinishedCallback = null);

        /// <summary>
        /// Moves to asynchronous.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns></returns>
        Task<bool> MoveToAsync(Vector3 position);
    }
}
