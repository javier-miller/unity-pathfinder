using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Executes pathfinding movement and publishes detailed lifecycle notifications.
    /// </summary>
    public interface IPathfinderMovement
    {
        event Action<PathfinderMovementNotification> MovementStarted;
        event Action<PathfinderMovementNotification> MovementReplanned;
        event Action<PathfinderMovementNotification> WaypointReached;
        event Action<PathfinderMovementNotification> MovementArrived;
        event Action<PathfinderMovementNotification> MovementBlocked;
        event Action<PathfinderMovementNotification> MovementFailed;
        event Action<PathfinderMovementNotification> MovementCancelled;

        PathfinderMovementState State { get; }
        int OperationId { get; }
        PathRequestPriority RequestPriority { get; set; }
        PathRequestHandle PendingPathRequest { get; }
        bool HasPendingMovement { get; }
        Vector3 RequestedDestination { get; }
        Vector3 ResolvedDestination { get; }
        bool HasResolvedDestination { get; }
        PathStatus LastPathStatus { get; }
        int LastExpandedNodeCount { get; }
        int LastPathCost { get; }
        long PathGridVersion { get; }
        int RepathCount { get; }
        PathRepathReason LastRepathReason { get; }
        float MinimumRepathInterval { get; }
        float RemainingRepathCooldown { get; }
        bool IsRepathCoolingDown { get; }
        Vector3 MovementDirection { get; }
        Vector2 ActualVelocity { get; }
        float ActualSpeed { get; }
        float WaypointTolerance { get; }
        float ArrivalTolerance { get; }

        /// <summary>
        /// Starts a movement with the default path-query options.
        /// </summary>
        bool MoveTo(Vector3 targetPosition);

        /// <summary>
        /// Starts a movement with explicit path-query options.
        /// </summary>
        bool MoveTo(Vector3 targetPosition, PathQueryOptions options);

        /// <summary>
        /// Starts following a successful path calculated for the agent's current
        /// position.
        /// </summary>
        bool FollowPrecomputedPath(
            IPathfinding pathfinder,
            PathResult pathResult,
            PathQueryOptions options);

        void RegisterVelocityModifier(
            IPathfinderMovementVelocityModifier modifier);

        bool UnregisterVelocityModifier(
            IPathfinderMovementVelocityModifier modifier);

        void CancelMovement();
        bool PauseMovement();
        bool ResumeMovement();

        bool RequestRepath(PathRepathReason reason = PathRepathReason.Manual);

        /// <summary>
        /// Starts a movement and completes with its detailed terminal snapshot.
        /// </summary>
        Task<PathfinderMovementNotification> MoveToAsync(Vector3 position);

        /// <summary>
        /// Starts a movement with explicit options and completes with its detailed
        /// terminal snapshot.
        /// </summary>
        Task<PathfinderMovementNotification> MoveToAsync(
            Vector3 position,
            PathQueryOptions options);
    }
}
