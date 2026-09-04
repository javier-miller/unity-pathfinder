using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Assigns compact, oriented and reachable destinations to a runtime selection
    /// of RTS units. Selection and pointer input remain outside this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sparky Games/Pathfinder/RTS Formation Destination Planner")]
    public sealed class RtsFormationDestinationPlanner : MonoBehaviour
    {
        private sealed class UnitAssignmentContext
        {
            public RtsUnitMovementController Unit;
            public Vector3 DesiredSlot;
            public Vector3 RequestedSlot;
            public Vector3 ResolvedSlot;
            public bool HasResolvedSlot;
            public Vector3 LastCandidateResolvedDestination;
            public bool HasLastCandidateResolvedDestination;
            public float LastCandidateFallbackDistance;
            public RtsFormationCandidateRejectionReason LastRejectionReason;
            public int AttemptCount;
            public PathStatus PathStatus;
            public RtsFormationAssignmentStatus Status;
            public PathQueryOptions QueryOptions;
            public PathRequestHandle Request;
            public int ExpectedMovementOperationId;
        }

        [SerializeField]
        private RtsFormationSettings settings = new RtsFormationSettings();

        private readonly List<UnitAssignmentContext> _contexts =
            new List<UnitAssignmentContext>();
        private readonly List<RtsFormationAssignment> _assignmentSnapshots =
            new List<RtsFormationAssignment>();
        private ReadOnlyCollection<RtsFormationAssignment> _assignmentView;
        private IPathfinding _pathfinder;
        private IPathRequestScheduler _scheduler;
        private Vector3 _center;
        private Vector2 _forward = Vector2.up;
        private Vector2 _right = Vector2.right;
        private int _operationId;

        public event Action<RtsFormationOrderNotification> AssignmentCompleted;

        public event Action<RtsFormationOrderNotification> AssignmentCancelled;

        public RtsFormationSettings Settings
        {
            get
            {
                settings ??= new RtsFormationSettings();
                return settings;
            }
        }

        public int OperationId => _operationId;

        public RtsFormationOrderState State { get; private set; } =
            RtsFormationOrderState.Idle;

        public bool IsAssigning => State == RtsFormationOrderState.Assigning;

        public IReadOnlyList<RtsFormationAssignment> CurrentAssignments
        {
            get
            {
                _assignmentView ??= _assignmentSnapshots.AsReadOnly();
                return _assignmentView;
            }
        }

        private void Awake()
        {
            _assignmentView = _assignmentSnapshots.AsReadOnly();
            Settings.Sanitize();
        }

        private void OnDisable() => CancelCurrentOrder();

        private void OnValidate()
        {
            settings ??= new RtsFormationSettings();
            settings.Sanitize();
        }

        /// <summary>
        /// Assigns a near-square formation facing world up.
        /// </summary>
        public bool IssueMoveOrder(
            IReadOnlyList<RtsUnitMovementController> selectedUnits,
            Vector3 formationCenter) =>
            IssueMoveOrder(selectedUnits, formationCenter, Vector2.up);

        /// <summary>
        /// Cancels previous orders for the supplied units, validates candidate slots
        /// through the central scheduler and starts each unit on its accepted route.
        /// </summary>
        public bool IssueMoveOrder(
            IReadOnlyList<RtsUnitMovementController> selectedUnits,
            Vector3 formationCenter,
            Vector2 formationForward)
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "The formation planner must be active and enabled.",
                    this);
                return false;
            }

            var units = CollectValidUnits(selectedUnits);
            if (units.Count == 0)
            {
                Debug.LogWarning(
                    "A formation order requires at least one active RTS unit.",
                    this);
                return false;
            }

            if (!PathfindingManager.TryGetInstance(out var pathfinder) ||
                !PathfindingManager.TryGetScheduler(out var scheduler))
            {
                Debug.LogWarning(
                    "A formation order requires an active pathfinder and scheduler.",
                    this);
                return false;
            }

            if (HasCancellableOrder())
            {
                var cancelledOperationId = _operationId;
                CancelCurrentOrder();
                if (_operationId != cancelledOperationId)
                {
                    // A cancellation listener created a newer formation order.
                    return false;
                }
            }

            PrepareOperation(
                pathfinder,
                scheduler,
                formationCenter,
                formationForward);
            BuildContexts(units);

            for (var i = 0; i < _contexts.Count; i++)
            {
                _contexts[i].Unit.CancelCurrentOrder();
                _contexts[i].ExpectedMovementOperationId =
                    _contexts[i].Unit.Movement.OperationId;
            }

            State = RtsFormationOrderState.Assigning;
            RefreshAssignmentSnapshots();
            var operationId = _operationId;
            for (var i = 0; i < _contexts.Count; i++)
            {
                RequestNextCandidate(_contexts[i], operationId);
            }

            TryCompleteAssignment(operationId);
            return State == RtsFormationOrderState.Assigning ||
                   State == RtsFormationOrderState.Assigned ||
                   State == RtsFormationOrderState.PartiallyAssigned;
        }

        /// <summary>
        /// Cancels queued slot searches and, by default, movements started by this order.
        /// </summary>
        public bool CancelCurrentOrder(bool cancelIssuedMovements = true)
        {
            if (!HasCancellableOrder())
            {
                return false;
            }

            var contexts = new List<UnitAssignmentContext>(_contexts);
            var requests = new List<PathRequestHandle>(contexts.Count);
            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                requests.Add(context.Request);
                context.Request = null;

                if (context.Status == RtsFormationAssignmentStatus.Searching)
                {
                    context.Status = RtsFormationAssignmentStatus.Cancelled;
                    context.PathStatus = PathStatus.Cancelled;
                }

            }

            State = RtsFormationOrderState.Cancelled;
            RefreshAssignmentSnapshots();
            var notification = CreateNotification();
            _pathfinder = null;
            _scheduler = null;

            // Cancelling a request or movement invokes external callbacks. All local
            // state is terminal before doing so, and iteration uses a snapshot so a
            // re-entrant order cannot be overwritten or accidentally traversed here.
            for (var i = 0; i < contexts.Count; i++)
            {
                requests[i]?.Cancel();
                if (cancelIssuedMovements &&
                    contexts[i].Status == RtsFormationAssignmentStatus.Assigned &&
                    contexts[i].Unit != null)
                {
                    contexts[i].Unit.CancelCurrentOrder();
                }
            }

            Publish(AssignmentCancelled, notification);
            return true;
        }

        private void PrepareOperation(
            IPathfinding pathfinder,
            IPathRequestScheduler scheduler,
            Vector3 formationCenter,
            Vector2 formationForward)
        {
            _operationId = _operationId == int.MaxValue ? 1 : _operationId + 1;
            _contexts.Clear();
            _assignmentSnapshots.Clear();
            _pathfinder = pathfinder;
            _scheduler = scheduler;
            _center = formationCenter;
            _forward = formationForward.sqrMagnitude > 0.0001f
                ? formationForward.normalized
                : Vector2.up;
            _right = new Vector2(_forward.y, -_forward.x);
            Settings.Sanitize();
            State = RtsFormationOrderState.Idle;
        }

        private void BuildContexts(
            IReadOnlyList<RtsUnitMovementController> units)
        {
            var desiredSlots = GenerateDesiredSlots(units.Count);
            var assignedUnits = new bool[units.Count];

            // Greedy nearest pairing is deterministic and avoids most unit crossing
            // without introducing a heavyweight optimal-assignment dependency.
            for (var slotIndex = 0; slotIndex < desiredSlots.Count; slotIndex++)
            {
                var bestUnitIndex = -1;
                var bestDistance = float.PositiveInfinity;
                for (var unitIndex = 0; unitIndex < units.Count; unitIndex++)
                {
                    if (assignedUnits[unitIndex])
                    {
                        continue;
                    }

                    var difference = (Vector2)(units[unitIndex].transform.position -
                                               desiredSlots[slotIndex]);
                    var distance = difference.sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestUnitIndex = unitIndex;
                    }
                }

                assignedUnits[bestUnitIndex] = true;
                _contexts.Add(new UnitAssignmentContext
                {
                    Unit = units[bestUnitIndex],
                    DesiredSlot = desiredSlots[slotIndex],
                    RequestedSlot = desiredSlots[slotIndex],
                    Status = RtsFormationAssignmentStatus.Searching,
                    PathStatus = PathStatus.Undefined
                });
            }
        }

        private List<Vector3> GenerateDesiredSlots(int unitCount)
        {
            var columns = Settings.Columns > 0
                ? Mathf.Min(Settings.Columns, unitCount)
                : Mathf.CeilToInt(Mathf.Sqrt(unitCount));
            var rows = Mathf.CeilToInt(unitCount / (float)columns);
            var slots = new List<Vector3>(unitCount);
            var remaining = unitCount;

            for (var row = 0; row < rows; row++)
            {
                var rowCount = Mathf.Min(columns, remaining);
                var localY = ((rows - 1) * 0.5f - row) * Settings.Spacing;
                for (var column = 0; column < rowCount; column++)
                {
                    var localX =
                        (column - (rowCount - 1) * 0.5f) * Settings.Spacing;
                    var offset = _right * localX + _forward * localY;
                    slots.Add(_center + new Vector3(offset.x, offset.y, 0f));
                }

                remaining -= rowCount;
            }

            return slots;
        }

        private void RequestNextCandidate(
            UnitAssignmentContext context,
            int operationId)
        {
            if (_operationId != operationId ||
                State != RtsFormationOrderState.Assigning)
            {
                return;
            }

            if (context.AttemptCount >= Settings.MaximumCandidateAttemptsPerUnit)
            {
                MarkFailed(context);
                TryCompleteAssignment(operationId);
                return;
            }

            context.HasLastCandidateResolvedDestination = false;
            context.LastCandidateResolvedDestination = default;
            context.LastCandidateFallbackDistance = 0f;
            context.LastRejectionReason =
                RtsFormationCandidateRejectionReason.None;
            context.RequestedSlot = GetCandidateSlot(
                context.DesiredSlot,
                context.AttemptCount);
            context.RequestedSlot = new Vector3(
                context.RequestedSlot.x,
                context.RequestedSlot.y,
                context.Unit.transform.position.z);
            context.AttemptCount++;
            context.QueryOptions = context.Unit.PathOptions.CreateQueryOptions();
            context.QueryOptions.FindNearestReachableDestination =
                Settings.FindNearestReachableSlot;
            context.Status = RtsFormationAssignmentStatus.Searching;
            RefreshAssignmentSnapshots();

            try
            {
                context.Request = _scheduler.Enqueue(
                    _pathfinder,
                    context.Unit.transform.position,
                    context.RequestedSlot,
                    context.QueryOptions,
                    context.Unit.Movement.RequestPriority,
                    (handle, result) =>
                        OnCandidateCompleted(operationId, context, handle, result));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                context.LastRejectionReason =
                    RtsFormationCandidateRejectionReason.PathUnavailable;
                MarkFailed(context);
                TryCompleteAssignment(operationId);
            }
        }

        private void OnCandidateCompleted(
            int operationId,
            UnitAssignmentContext context,
            PathRequestHandle request,
            PathResult result)
        {
            if (_operationId != operationId ||
                State != RtsFormationOrderState.Assigning ||
                !ReferenceEquals(context.Request, request))
            {
                return;
            }

            context.Request = null;
            context.PathStatus = result?.Status ?? PathStatus.InvalidConfiguration;
            var movement = context.Unit?.Movement;
            if (movement == null ||
                movement.OperationId != context.ExpectedMovementOperationId ||
                movement.HasPendingMovement)
            {
                // A newer per-unit command owns this agent. The older group order
                // must never overwrite it when its path result arrives later.
                context.Status = RtsFormationAssignmentStatus.Cancelled;
                context.PathStatus = PathStatus.Cancelled;
                context.LastRejectionReason =
                    RtsFormationCandidateRejectionReason.MovementSuperseded;
                RefreshAssignmentSnapshots();
                TryCompleteAssignment(operationId);
                return;
            }

            if (result?.Status == PathStatus.Cancelled)
            {
                context.LastRejectionReason =
                    RtsFormationCandidateRejectionReason.PathUnavailable;
                MarkFailed(context);
                TryCompleteAssignment(operationId);
                return;
            }

            if (result != null &&
                result.Succeeded &&
                result.HasResolvedDestination)
            {
                context.LastCandidateResolvedDestination =
                    result.ResolvedDestination;
                context.HasLastCandidateResolvedDestination = true;
                context.LastCandidateFallbackDistance = Vector2.Distance(
                    context.RequestedSlot,
                    result.ResolvedDestination);

                if (!IsFallbackDistanceAccepted(context, result))
                {
                    context.LastRejectionReason =
                        RtsFormationCandidateRejectionReason.FallbackTooFar;
                    RequestNextCandidate(context, operationId);
                    return;
                }

                if (!IsResolvedSlotAvailable(
                        context,
                        result.ResolvedDestination))
                {
                    context.LastRejectionReason =
                        RtsFormationCandidateRejectionReason.DestinationAlreadyReserved;
                    RequestNextCandidate(context, operationId);
                    return;
                }

                context.ResolvedSlot = result.ResolvedDestination;
                context.HasResolvedSlot = true;
                context.LastRejectionReason =
                    RtsFormationCandidateRejectionReason.None;
                context.Status = RtsFormationAssignmentStatus.Assigned;
                RefreshAssignmentSnapshots();

                if (!movement.FollowPrecomputedPath(
                        _pathfinder,
                        result,
                        context.QueryOptions))
                {
                    context.LastRejectionReason =
                        RtsFormationCandidateRejectionReason.MovementRejected;
                    MarkFailed(context);
                }

                TryCompleteAssignment(operationId);
                return;
            }

            context.HasLastCandidateResolvedDestination = false;
            context.LastCandidateResolvedDestination = default;
            context.LastCandidateFallbackDistance = 0f;
            context.LastRejectionReason =
                RtsFormationCandidateRejectionReason.PathUnavailable;
            RequestNextCandidate(context, operationId);
        }

        private bool IsFallbackDistanceAccepted(
            UnitAssignmentContext context,
            PathResult result)
        {
            if (result.Status != PathStatus.SuccessNearestReachable)
            {
                return true;
            }

            var maximumDistance = Settings.MaximumFallbackDistance;
            return maximumDistance <= 0f ||
                   context.LastCandidateFallbackDistance <= maximumDistance;
        }

        private bool IsResolvedSlotAvailable(
            UnitAssignmentContext owner,
            Vector3 resolvedSlot)
        {
            var minimumSeparation = Settings.MinimumResolvedSlotSeparation;
            var minimumSquared = minimumSeparation * minimumSeparation;
            for (var i = 0; i < _contexts.Count; i++)
            {
                var other = _contexts[i];
                if (ReferenceEquals(owner, other) || !other.HasResolvedSlot)
                {
                    continue;
                }

                var difference = (Vector2)(resolvedSlot - other.ResolvedSlot);
                if (difference.sqrMagnitude < minimumSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 GetCandidateSlot(Vector3 desiredSlot, int attemptIndex)
        {
            var spiralOffset = GetSpiralOffset(attemptIndex);
            var worldOffset =
                _right * (spiralOffset.x * Settings.CandidateSearchStep) +
                _forward * (spiralOffset.y * Settings.CandidateSearchStep);
            return desiredSlot + new Vector3(worldOffset.x, worldOffset.y, 0f);
        }

        private static Vector2Int GetSpiralOffset(int index)
        {
            if (index <= 0)
            {
                return Vector2Int.zero;
            }

            var x = 0;
            var y = 0;
            var directionX = 1;
            var directionY = 0;
            var segmentLength = 1;
            var segmentProgress = 0;
            var completedSegments = 0;

            for (var step = 0; step < index; step++)
            {
                x += directionX;
                y += directionY;
                segmentProgress++;
                if (segmentProgress < segmentLength)
                {
                    continue;
                }

                segmentProgress = 0;
                var previousDirectionX = directionX;
                directionX = -directionY;
                directionY = previousDirectionX;
                completedSegments++;
                if (completedSegments % 2 == 0)
                {
                    segmentLength++;
                }
            }

            return new Vector2Int(x, y);
        }

        private void MarkFailed(UnitAssignmentContext context)
        {
            context.Request = null;
            context.HasResolvedSlot = false;
            context.ResolvedSlot = default;
            context.Status = RtsFormationAssignmentStatus.Failed;
            if (context.PathStatus == PathStatus.Undefined)
            {
                context.PathStatus = PathStatus.Unreachable;
            }

            RefreshAssignmentSnapshots();
        }

        private void TryCompleteAssignment(int operationId)
        {
            if (_operationId != operationId ||
                State != RtsFormationOrderState.Assigning)
            {
                return;
            }

            var assigned = 0;
            for (var i = 0; i < _contexts.Count; i++)
            {
                switch (_contexts[i].Status)
                {
                    case RtsFormationAssignmentStatus.Searching:
                        return;
                    case RtsFormationAssignmentStatus.Assigned:
                        assigned++;
                        break;
                }
            }

            State = assigned == _contexts.Count
                ? RtsFormationOrderState.Assigned
                : assigned > 0
                    ? RtsFormationOrderState.PartiallyAssigned
                    : RtsFormationOrderState.Failed;
            RefreshAssignmentSnapshots();
            Publish(AssignmentCompleted, CreateNotification());
        }

        private void RefreshAssignmentSnapshots()
        {
            _assignmentSnapshots.Clear();
            for (var i = 0; i < _contexts.Count; i++)
            {
                var context = _contexts[i];
                _assignmentSnapshots.Add(new RtsFormationAssignment(
                    context.Unit,
                    context.DesiredSlot,
                    context.RequestedSlot,
                    context.ResolvedSlot,
                    context.HasResolvedSlot,
                    context.LastCandidateResolvedDestination,
                    context.HasLastCandidateResolvedDestination,
                    context.LastCandidateFallbackDistance,
                    context.LastRejectionReason,
                    context.Status,
                    context.PathStatus,
                    context.AttemptCount));
            }
        }

        private RtsFormationOrderNotification CreateNotification() =>
            new RtsFormationOrderNotification(
                _operationId,
                State,
                _center,
                _forward,
                CurrentAssignments);

        private void Publish(
            Action<RtsFormationOrderNotification> handlers,
            RtsFormationOrderNotification notification)
        {
            if (handlers == null)
            {
                return;
            }

            try
            {
                handlers.Invoke(notification);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private bool HasCancellableOrder() =>
            State == RtsFormationOrderState.Assigning ||
            State == RtsFormationOrderState.Assigned ||
            State == RtsFormationOrderState.PartiallyAssigned;

        private static List<RtsUnitMovementController> CollectValidUnits(
            IReadOnlyList<RtsUnitMovementController> selectedUnits)
        {
            var units = new List<RtsUnitMovementController>();
            if (selectedUnits == null)
            {
                return units;
            }

            var unique = new HashSet<RtsUnitMovementController>();
            for (var i = 0; i < selectedUnits.Count; i++)
            {
                var unit = selectedUnits[i];
                if (unit == null ||
                    !unit.isActiveAndEnabled ||
                    unit.Movement == null ||
                    !unit.Movement.isActiveAndEnabled ||
                    !unique.Add(unit))
                {
                    continue;
                }

                units.Add(unit);
            }

            return units;
        }
    }
}
