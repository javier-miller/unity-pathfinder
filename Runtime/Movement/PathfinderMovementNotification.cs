using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Immutable snapshot published by a <see cref="PathfinderMovement"/> notification.
    /// </summary>
    public sealed class PathfinderMovementNotification
    {
        internal PathfinderMovementNotification(
            int operationId,
            PathfinderMovementState state,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            bool hasResolvedDestination,
            PathStatus pathStatus,
            int expandedNodeCount,
            int pathCost,
            Vector2 position,
            Vector2 actualVelocity,
            int waypointIndex,
            int waypointCount,
            Vector3 waypoint,
            bool hasWaypoint,
            long gridVersion,
            int repathCount,
            PathRepathReason repathReason)
        {
            OperationId = operationId;
            State = state;
            RequestedDestination = requestedDestination;
            ResolvedDestination = resolvedDestination;
            HasResolvedDestination = hasResolvedDestination;
            PathStatus = pathStatus;
            ExpandedNodeCount = expandedNodeCount;
            PathCost = pathCost;
            Position = position;
            ActualVelocity = actualVelocity;
            WaypointIndex = waypointIndex;
            WaypointCount = waypointCount;
            Waypoint = waypoint;
            HasWaypoint = hasWaypoint;
            GridVersion = gridVersion;
            RepathCount = repathCount;
            RepathReason = repathReason;
        }

        /// <summary>
        /// Gets the monotonically increasing identifier assigned by the movement component.
        /// </summary>
        public int OperationId { get; }

        /// <summary>
        /// Gets the movement state at the time the notification was created.
        /// </summary>
        public PathfinderMovementState State { get; }

        public Vector3 RequestedDestination { get; }

        public Vector3 ResolvedDestination { get; }

        public bool HasResolvedDestination { get; }

        public PathStatus PathStatus { get; }

        public int ExpandedNodeCount { get; }

        public int PathCost { get; }

        /// <summary>
        /// Gets the agent position observed when the notification was created.
        /// </summary>
        public Vector2 Position { get; }

        /// <summary>
        /// Gets the velocity observed when the notification was created.
        /// </summary>
        public Vector2 ActualVelocity { get; }

        public float ActualSpeed => ActualVelocity.magnitude;

        /// <summary>
        /// Gets the zero-based waypoint index, or -1 when the notification is not for a waypoint.
        /// </summary>
        public int WaypointIndex { get; }

        public int WaypointCount { get; }

        public Vector3 Waypoint { get; }

        public bool HasWaypoint { get; }

        /// <summary>
        /// Gets the navigation version used by the current retained path.
        /// </summary>
        public long GridVersion { get; }

        /// <summary>
        /// Gets the number of replacement routes in this operation.
        /// </summary>
        public int RepathCount { get; }

        public PathRepathReason RepathReason { get; }
    }
}
