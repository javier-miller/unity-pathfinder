using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    public enum RtsFormationAssignmentStatus
    {
        Searching = 0,
        Assigned = 1,
        Failed = 2,
        Cancelled = 3
    }

    public enum RtsFormationOrderState
    {
        Idle = 0,
        Assigning = 1,
        Assigned = 2,
        PartiallyAssigned = 3,
        Failed = 4,
        Cancelled = 5
    }

    /// <summary>
    /// Explains why the most recently evaluated candidate was not assigned.
    /// </summary>
    public enum RtsFormationCandidateRejectionReason
    {
        None = 0,
        PathUnavailable = 1,
        FallbackTooFar = 2,
        DestinationAlreadyReserved = 3,
        MovementSuperseded = 4,
        MovementRejected = 5
    }

    /// <summary>
    /// Immutable snapshot of one unit's slot-assignment attempt.
    /// </summary>
    public sealed class RtsFormationAssignment
    {
        internal RtsFormationAssignment(
            RtsUnitMovementController unit,
            Vector3 desiredSlot,
            Vector3 requestedSlot,
            Vector3 resolvedSlot,
            bool hasResolvedSlot,
            Vector3 lastCandidateResolvedDestination,
            bool hasLastCandidateResolvedDestination,
            float lastCandidateFallbackDistance,
            RtsFormationCandidateRejectionReason lastRejectionReason,
            RtsFormationAssignmentStatus status,
            PathStatus pathStatus,
            int attemptCount)
        {
            Unit = unit;
            DesiredSlot = desiredSlot;
            RequestedSlot = requestedSlot;
            ResolvedSlot = resolvedSlot;
            HasResolvedSlot = hasResolvedSlot;
            LastCandidateResolvedDestination =
                lastCandidateResolvedDestination;
            HasLastCandidateResolvedDestination =
                hasLastCandidateResolvedDestination;
            LastCandidateFallbackDistance = lastCandidateFallbackDistance;
            LastRejectionReason = lastRejectionReason;
            Status = status;
            PathStatus = pathStatus;
            AttemptCount = attemptCount;
        }

        public RtsUnitMovementController Unit { get; }

        public Vector3 DesiredSlot { get; }

        public Vector3 RequestedSlot { get; }

        public Vector3 ResolvedSlot { get; }

        public bool HasResolvedSlot { get; }

        /// <summary>
        /// Gets the destination returned for the most recently completed candidate,
        /// including a fallback that was subsequently rejected.
        /// </summary>
        public Vector3 LastCandidateResolvedDestination { get; }

        public bool HasLastCandidateResolvedDestination { get; }

        public float LastCandidateFallbackDistance { get; }

        public RtsFormationCandidateRejectionReason LastRejectionReason { get; }

        public RtsFormationAssignmentStatus Status { get; }

        public PathStatus PathStatus { get; }

        public int AttemptCount { get; }
    }

    /// <summary>
    /// Immutable summary emitted after all units have been assigned or exhausted.
    /// </summary>
    public sealed class RtsFormationOrderNotification
    {
        internal RtsFormationOrderNotification(
            int operationId,
            RtsFormationOrderState state,
            Vector3 center,
            Vector2 forward,
            IReadOnlyList<RtsFormationAssignment> assignments)
        {
            OperationId = operationId;
            State = state;
            Center = center;
            Forward = forward;

            var copy = new List<RtsFormationAssignment>(assignments);
            Assignments = new ReadOnlyCollection<RtsFormationAssignment>(copy);
            for (var i = 0; i < copy.Count; i++)
            {
                switch (copy[i].Status)
                {
                    case RtsFormationAssignmentStatus.Assigned:
                        AssignedCount++;
                        break;
                    case RtsFormationAssignmentStatus.Failed:
                        FailedCount++;
                        break;
                    case RtsFormationAssignmentStatus.Cancelled:
                        CancelledCount++;
                        break;
                }
            }
        }

        public int OperationId { get; }

        public RtsFormationOrderState State { get; }

        public Vector3 Center { get; }

        public Vector2 Forward { get; }

        public IReadOnlyList<RtsFormationAssignment> Assignments { get; }

        public int AssignedCount { get; private set; }

        public int FailedCount { get; private set; }

        public int CancelledCount { get; private set; }
    }
}
