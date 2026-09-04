using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Follows paths produced by the active pathfinder and exposes an explicit
    /// movement-operation state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PathfinderMovement : MonoBehaviour, IPathfinderMovement
    {
        private const float MinimumPositiveValue = 0.001f;

        private readonly List<Vector3> _pathVectorList = new List<Vector3>();
        private readonly List<IPathfinderMovementVelocityModifier> _velocityModifiers =
            new List<IPathfinderMovementVelocityModifier>();

        private Rigidbody2D _rigidbody;
        private Vector2 _targetNextPosition;
        private Vector2 _currentPosition;
        private Vector2 _lastPhysicsPosition;
        private Vector2 _actualVelocity;
        private bool _hasPhysicsPositionSample;
        private int _currentPathIndex;
        private int _operationId;
        private int _operationWaypointCount;
        private int _repathCount;
        private long _pathGridVersion;
        private Vector3 _movementDirection;
        private PathfinderMovementState _state = PathfinderMovementState.Idle;
        private TaskCompletionSource<PathfinderMovementNotification>
            _movementCompletionSource;
        private IPathfinding _activePathfinder;
        private IVersionedPathfinding _versionedPathfinder;
        private PathQueryOptions _activePathOptions;
        private PathRequestHandle _pathRequestHandle;
        private bool _pendingRequestIsReplan;
        private bool _repathPendingAfterCooldown;
        private float _nextAllowedRepathTime;
        private PathRepathReason _pendingRepathReason;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        private float speed = 4f;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        private float waypointTolerance = 0.1f;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        private float arrivalTolerance = 0.1f;

        [SerializeField]
        private PathRequestPriority requestPriority = PathRequestPriority.Normal;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum unscaled time between replacement route requests.")]
        private float minimumRepathInterval = 0.5f;

        public event Action<PathfinderMovementNotification> MovementStarted;

        public event Action<PathfinderMovementNotification> MovementReplanned;

        public event Action<PathfinderMovementNotification> WaypointReached;

        public event Action<PathfinderMovementNotification> MovementArrived;

        public event Action<PathfinderMovementNotification> MovementBlocked;

        public event Action<PathfinderMovementNotification> MovementFailed;

        public event Action<PathfinderMovementNotification> MovementCancelled;

        /// <summary>
        /// Gets the lifecycle state of the current or most recently completed operation.
        /// </summary>
        public PathfinderMovementState State => _state;

        /// <summary>
        /// Gets the identifier of the current or most recently started operation.
        /// </summary>
        public int OperationId => _operationId;

        /// <summary>
        /// Gets or sets the priority used by initial path requests and replans.
        /// </summary>
        public PathRequestPriority RequestPriority
        {
            get => requestPriority;
            set => requestPriority = SanitizePriority(value);
        }

        /// <summary>
        /// Gets the current scheduled request while waiting for a result.
        /// </summary>
        public PathRequestHandle PendingPathRequest => _pathRequestHandle;

        /// <summary>
        /// Gets whether the rigidbody is currently advancing along its path.
        /// </summary>
        public bool IsActive => _state == PathfinderMovementState.FollowingPath;

        /// <summary>
        /// Gets whether an operation is queued, following or retaining a paused path.
        /// </summary>
        public bool HasPendingMovement =>
            _state == PathfinderMovementState.WaitingForPath ||
            _state == PathfinderMovementState.FollowingPath ||
            _state == PathfinderMovementState.Paused;

        /// <summary>
        /// Gets the destination supplied by the caller for the current or last operation.
        /// </summary>
        public Vector3 RequestedDestination { get; private set; }

        /// <summary>
        /// Gets the destination selected by navigation for the current or last operation.
        /// It can differ from <see cref="RequestedDestination"/> when nearest-reachable
        /// destination fallback is enabled.
        /// </summary>
        public Vector3 ResolvedDestination { get; private set; }

        /// <summary>
        /// Gets whether <see cref="ResolvedDestination"/> is meaningful.
        /// </summary>
        public bool HasResolvedDestination { get; private set; }

        /// <summary>
        /// Gets the path-query status associated with the current or last operation.
        /// </summary>
        public PathStatus LastPathStatus { get; private set; } = PathStatus.Undefined;

        /// <summary>
        /// Gets the number of nodes expanded by the current or last path query.
        /// </summary>
        public int LastExpandedNodeCount { get; private set; }

        /// <summary>
        /// Gets the accumulated cost of the current or most recent successful route.
        /// </summary>
        public int LastPathCost { get; private set; }

        /// <summary>
        /// Gets the grid version against which the retained path was last validated.
        /// </summary>
        public long PathGridVersion => _pathGridVersion;

        /// <summary>
        /// Gets the number of replacement routes made by the current operation.
        /// </summary>
        public int RepathCount => _repathCount;

        public PathRepathReason LastRepathReason { get; private set; }

        public float MinimumRepathInterval => minimumRepathInterval;

        public float RemainingRepathCooldown => Mathf.Max(
            0f,
            _nextAllowedRepathTime - Time.unscaledTime);

        public bool IsRepathCoolingDown => RemainingRepathCooldown > 0f;

        /// <summary>
        /// Gets the normalized direction towards the next waypoint while moving.
        /// </summary>
        public Vector3 MovementDirection => _movementDirection;

        /// <summary>
        /// Gets the velocity measured from Rigidbody2D displacement between physics steps.
        /// Unlike <see cref="MovementDirection"/>, this reflects observed motion.
        /// </summary>
        public Vector2 ActualVelocity => _actualVelocity;

        /// <summary>
        /// Gets the magnitude of <see cref="ActualVelocity"/> in world units per second.
        /// </summary>
        public float ActualSpeed => _actualVelocity.magnitude;

        /// <summary>
        /// Gets the configured tolerance used for intermediate waypoints.
        /// </summary>
        public float WaypointTolerance => waypointTolerance;

        /// <summary>
        /// Gets the configured tolerance used for the final resolved destination.
        /// </summary>
        public float ArrivalTolerance => arrivalTolerance;

        /// <summary>
        /// Gets the position last observed by the state machine.
        /// </summary>
        public Vector2 CurrentPosition => _currentPosition;

        /// <summary>
        /// Gets the next waypoint, when an operation has one.
        /// </summary>
        public Vector3 NextWaypoint =>
            _currentPathIndex >= 0 && _currentPathIndex < _pathVectorList.Count
                ? _pathVectorList[_currentPathIndex]
                : default;

        /// <summary>
        /// Gets the index of the next waypoint.
        /// </summary>
        public int CurrentPathIndex => _currentPathIndex;

        /// <summary>
        /// Gets the path retained by the active or paused operation.
        /// </summary>
        public IReadOnlyList<Vector3> CurrentPath => _pathVectorList;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _currentPosition = _rigidbody != null
                ? _rigidbody.position
                : (Vector2)transform.position;
            ResetVelocitySample();
            TransitionTo(PathfinderMovementState.Idle);
        }

        private void OnEnable() => ResetVelocitySample();

        private void OnDisable()
        {
            CancelMovement();
            ResetVelocitySample();
        }

        private void OnValidate()
        {
            speed = Mathf.Max(MinimumPositiveValue, speed);
            waypointTolerance = Mathf.Max(MinimumPositiveValue, waypointTolerance);
            arrivalTolerance = Mathf.Max(MinimumPositiveValue, arrivalTolerance);
            minimumRepathInterval = Mathf.Max(0f, minimumRepathInterval);
            requestPriority = SanitizePriority(requestPriority);
        }

        private void Update()
        {
            if (_state != PathfinderMovementState.FollowingPath)
            {
                return;
            }

            _currentPosition = _rigidbody != null
                ? _rigidbody.position
                : (Vector2)transform.position;
            if (!EnsurePathMatchesCurrentGrid())
            {
                return;
            }

            AdvancePathIfNeeded();
        }

        private void FixedUpdate()
        {
            if (_rigidbody == null)
            {
                _actualVelocity = Vector2.zero;
                return;
            }

            var currentPosition = _rigidbody.position;
            SampleActualVelocity(currentPosition);
            _currentPosition = currentPosition;
            if (_state != PathfinderMovementState.FollowingPath)
            {
                return;
            }

            if (!EnsurePathMatchesCurrentGrid())
            {
                return;
            }

            var difference = _targetNextPosition - currentPosition;
            var currentTolerance = GetToleranceForWaypoint(_currentPathIndex);
            if (difference.sqrMagnitude <= currentTolerance * currentTolerance)
            {
                return;
            }

            _movementDirection = difference.normalized;
            var desiredVelocity = (Vector2)_movementDirection * speed;
            var movementVelocity = ApplyVelocityModifiers(
                desiredVelocity,
                Time.fixedDeltaTime);
            if (movementVelocity.sqrMagnitude > speed * speed)
            {
                movementVelocity = movementVelocity.normalized * speed;
            }

            Vector2 nextPosition;
            if ((movementVelocity - desiredVelocity).sqrMagnitude <= 0.000001f)
            {
                // Keep exact waypoint snapping when no modifier changes the velocity.
                nextPosition = Vector2.MoveTowards(
                    currentPosition,
                    _targetNextPosition,
                    speed * Time.fixedDeltaTime);
            }
            else
            {
                nextPosition = currentPosition +
                               movementVelocity * Time.fixedDeltaTime;
            }

            _rigidbody.MovePosition(nextPosition);
        }

        /// <summary>
        /// Sets the movement speed, clamped to a positive value.
        /// </summary>
        public void SetSpeed(float value)
        {
            speed = Mathf.Max(MinimumPositiveValue, value);
        }

        /// <summary>
        /// Sets the tolerance for intermediate waypoints.
        /// </summary>
        public void SetWaypointTolerance(float value)
        {
            waypointTolerance = Mathf.Max(MinimumPositiveValue, value);
        }

        /// <summary>
        /// Sets the tolerance for the final resolved destination.
        /// </summary>
        public void SetArrivalTolerance(float value)
        {
            arrivalTolerance = Mathf.Max(MinimumPositiveValue, value);
        }

        /// <summary>
        /// Changes the minimum interval between replacement route requests.
        /// </summary>
        public void SetMinimumRepathInterval(float seconds)
        {
            minimumRepathInterval = Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// Registers an optional steering modifier. Ordered modifiers execute from
        /// the lowest to the highest order. Equal modifiers retain registration order.
        /// </summary>
        public void RegisterVelocityModifier(
            IPathfinderMovementVelocityModifier modifier)
        {
            if (modifier == null || _velocityModifiers.Contains(modifier))
            {
                return;
            }

            var modifierOrder = ResolveVelocityModifierOrder(modifier);
            var insertionIndex = _velocityModifiers.Count;
            for (var index = 0; index < _velocityModifiers.Count; index++)
            {
                if (ResolveVelocityModifierOrder(_velocityModifiers[index]) >
                    modifierOrder)
                {
                    insertionIndex = index;
                    break;
                }
            }

            _velocityModifiers.Insert(insertionIndex, modifier);
        }

        /// <summary>
        /// Removes a previously registered steering modifier.
        /// </summary>
        public bool UnregisterVelocityModifier(
            IPathfinderMovementVelocityModifier modifier) =>
            modifier != null && _velocityModifiers.Remove(modifier);

        /// <summary>
        /// Gets the actual execution index of a registered modifier, or -1 when
        /// it is not registered. Intended for setup diagnostics.
        /// </summary>
        public int GetVelocityModifierExecutionIndex(
            IPathfinderMovementVelocityModifier modifier) =>
            modifier == null ? -1 : _velocityModifiers.IndexOf(modifier);

        /// <summary>
        /// Starts a movement with the default path-query options.
        /// A new order cancels a previous queued, active or paused order first.
        /// </summary>
        public bool MoveTo(Vector3 targetPosition) =>
            StartMovement(targetPosition, PathQueryOptions.Default, null);

        /// <summary>
        /// Starts a movement with explicit path-query options.
        /// </summary>
        public bool MoveTo(
            Vector3 targetPosition,
            PathQueryOptions options) =>
            StartMovement(targetPosition, options, null);

        private bool StartMovement(
            Vector3 targetPosition,
            PathQueryOptions options,
            TaskCompletionSource<PathfinderMovementNotification> completionSource)
        {
            if (HasPendingMovement)
            {
                var cancelledOperationId = _operationId;
                CancelMovement();
                if (_operationId != cancelledOperationId)
                {
                    // A cancellation listener issued a newer order. The newest
                    // re-entrant order owns the component and must not be overwritten.
                    completionSource?.TrySetCanceled();
                    return false;
                }
            }

            PrepareNewOperation(targetPosition, completionSource);

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("PathfinderMovement must be active and enabled before moving.", this);
                LastPathStatus = PathStatus.InvalidConfiguration;
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }

            if (!PathfindingManager.TryGetInstance(out var pathfinder))
            {
                Debug.LogWarning("Pathfinder is not defined.", this);
                LastPathStatus = PathStatus.InvalidConfiguration;
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }

            _activePathfinder = pathfinder;
            _versionedPathfinder = pathfinder as IVersionedPathfinding;
            _activePathOptions = options?.Clone() ?? PathQueryOptions.Default;

            if (PathfindingManager.TryGetScheduler(out var scheduler))
            {
                return QueuePathRequest(
                    scheduler,
                    transform.position,
                    isReplan: false);
            }

            return CalculateInitialPathSynchronously(transform.position);
        }

        /// <summary>
        /// Starts following a path that was just calculated for this agent's current
        /// position. The caller must keep the agent stationary between requesting and
        /// supplying the result. Grid-version invalidation remains active afterwards.
        /// </summary>
        public bool FollowPrecomputedPath(
            IPathfinding pathfinder,
            PathResult pathResult,
            PathQueryOptions options)
        {
            if (HasPendingMovement)
            {
                var cancelledOperationId = _operationId;
                CancelMovement();
                if (_operationId != cancelledOperationId)
                {
                    return false;
                }
            }

            PrepareNewOperation(
                pathResult?.RequestedDestination ?? transform.position,
                null);

            if (!isActiveAndEnabled || pathfinder == null || pathResult == null)
            {
                Debug.LogWarning(
                    "A precomputed movement requires an active component, pathfinder and result.",
                    this);
                LastPathStatus = PathStatus.InvalidConfiguration;
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }

            _activePathfinder = pathfinder;
            _versionedPathfinder = pathfinder as IVersionedPathfinding;
            _activePathOptions = options?.Clone() ?? PathQueryOptions.Default;
            CapturePathResult(pathResult);
            if (!pathResult.Succeeded)
            {
                CompleteMovement(MapFailureState(LastPathStatus));
                return false;
            }

            return StartFollowingPath(pathResult.Waypoints);
        }

        /// <summary>
        /// Waits asynchronously for a movement made with default query options.
        /// The central scheduler queues the synchronous path calculation by frame.
        /// </summary>
        public Task<PathfinderMovementNotification> MoveToAsync(Vector3 position) =>
            MoveToAsync(position, PathQueryOptions.Default);

        /// <summary>
        /// Waits asynchronously for a movement made with explicit query options.
        /// The central scheduler queues the synchronous path calculation by frame.
        /// </summary>
        public Task<PathfinderMovementNotification> MoveToAsync(
            Vector3 position,
            PathQueryOptions options)
        {
            var completionSource =
                new TaskCompletionSource<PathfinderMovementNotification>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            if (!StartMovement(position, options, completionSource) &&
                !completionSource.Task.IsCompleted)
            {
                completionSource.TrySetCanceled();
            }

            return completionSource.Task;
        }

        /// <summary>
        /// Pauses the current path without completing its callback or task.
        /// </summary>
        public bool PauseMovement()
        {
            if (_state != PathfinderMovementState.FollowingPath)
            {
                return false;
            }

            _movementDirection = Vector3.zero;
            TransitionTo(PathfinderMovementState.Paused);
            ResetVelocitySample();
            return true;
        }

        /// <summary>
        /// Resumes a previously paused operation.
        /// </summary>
        public bool ResumeMovement()
        {
            if (_state != PathfinderMovementState.Paused || _pathVectorList.Count == 0)
            {
                return false;
            }

            _currentPosition = _rigidbody != null
                ? _rigidbody.position
                : (Vector2)transform.position;
            TransitionTo(PathfinderMovementState.FollowingPath);
            ResetVelocitySample();
            if (!EnsurePathMatchesCurrentGrid() ||
                _state != PathfinderMovementState.FollowingPath)
            {
                return _state == PathfinderMovementState.Arrived;
            }

            _targetNextPosition = _pathVectorList[_currentPathIndex];
            _movementDirection = (_targetNextPosition - _currentPosition).normalized;
            AdvancePathIfNeeded();
            return true;
        }

        /// <summary>
        /// Requests a replacement route without starting a new movement operation.
        /// During cooldown the current route is retained but movement is stopped.
        /// </summary>
        public bool RequestRepath(PathRepathReason reason = PathRepathReason.Manual)
        {
            if (_state != PathfinderMovementState.FollowingPath ||
                _activePathfinder == null ||
                _activePathOptions == null ||
                _repathPendingAfterCooldown)
            {
                return false;
            }

            _pendingRepathReason = reason == PathRepathReason.None
                ? PathRepathReason.Manual
                : reason;
            _repathPendingAfterCooldown = true;
            _movementDirection = Vector3.zero;
            TryStartPendingRepath();
            return true;
        }

        /// <summary>
        /// Cancels the current movement, if any.
        /// </summary>
        public void CancelMovement()
        {
            if (!HasPendingMovement)
            {
                return;
            }

            CompleteMovement(PathfinderMovementState.Cancelled);
        }

        private void PrepareNewOperation(
            Vector3 requestedDestination,
            TaskCompletionSource<PathfinderMovementNotification> completionSource)
        {
            _operationId = _operationId == int.MaxValue ? 1 : _operationId + 1;
            _pathVectorList.Clear();
            _currentPathIndex = 0;
            _operationWaypointCount = 0;
            _repathCount = 0;
            _pathGridVersion = 0;
            _targetNextPosition = default;
            _movementDirection = Vector3.zero;
            _movementCompletionSource = completionSource;
            _activePathfinder = null;
            _versionedPathfinder = null;
            _activePathOptions = null;
            _pathRequestHandle = null;
            _pendingRequestIsReplan = false;
            _repathPendingAfterCooldown = false;
            _pendingRepathReason = PathRepathReason.None;
            _nextAllowedRepathTime = 0f;
            LastRepathReason = PathRepathReason.None;
            RequestedDestination = requestedDestination;
            ResolvedDestination = default;
            HasResolvedDestination = false;
            LastPathStatus = PathStatus.Undefined;
            LastExpandedNodeCount = 0;
            LastPathCost = 0;
            RefreshCurrentPosition();
            ResetVelocitySample();
            TransitionTo(PathfinderMovementState.Idle);
        }

        private void ApplyPathResult(PathResult pathResult)
        {
            if (pathResult == null)
            {
                LastPathStatus = PathStatus.InvalidConfiguration;
                return;
            }

            RequestedDestination = pathResult.RequestedDestination;
            ResolvedDestination = pathResult.ResolvedDestination;
            HasResolvedDestination = pathResult.HasResolvedDestination;
            LastPathStatus = pathResult.Status;
            LastExpandedNodeCount = pathResult.ExpandedNodeCount;
            LastPathCost = pathResult.TotalCost;
        }

        private bool StartFollowingPath(IReadOnlyList<Vector3> path)
        {
            if (!TryStorePath(path, out var alreadyAtDestination))
            {
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }

            TransitionTo(PathfinderMovementState.FollowingPath);
            var startedOperationId = _operationId;
            PublishNotification(
                MovementStarted,
                CreateNotification());

            if (_operationId != startedOperationId ||
                _state == PathfinderMovementState.Cancelled ||
                _state == PathfinderMovementState.Blocked ||
                _state == PathfinderMovementState.Failed)
            {
                return false;
            }

            if (alreadyAtDestination)
            {
                CompleteMovement(PathfinderMovementState.Arrived);
                return true;
            }

            if (_state == PathfinderMovementState.Paused)
            {
                return true;
            }

            AdvancePathIfNeeded();
            return _operationId == startedOperationId &&
                   _state != PathfinderMovementState.Cancelled;
        }

        private bool EnsurePathMatchesCurrentGrid()
        {
            if (_repathPendingAfterCooldown)
            {
                return TryStartPendingRepath();
            }

            if (_versionedPathfinder == null ||
                _activePathfinder == null ||
                _versionedPathfinder.GridVersion == _pathGridVersion)
            {
                return true;
            }

            if (LastPathStatus != PathStatus.SuccessNearestReachable &&
                _versionedPathfinder.IsPathWalkable(
                    _currentPosition,
                    _pathVectorList,
                    _currentPathIndex,
                    _pathGridVersion,
                    _activePathOptions))
            {
                _pathGridVersion = _versionedPathfinder.GridVersion;
                return true;
            }

            _pendingRepathReason = PathRepathReason.GridInvalidated;
            _repathPendingAfterCooldown = true;
            _movementDirection = Vector3.zero;
            return TryStartPendingRepath();
        }

        private bool TryStartPendingRepath()
        {
            if (!_repathPendingAfterCooldown ||
                Time.unscaledTime < _nextAllowedRepathTime)
            {
                return false;
            }

            var reason = _pendingRepathReason;
            _repathPendingAfterCooldown = false;
            _pendingRepathReason = PathRepathReason.None;
            return RecalculatePath(reason);
        }

        private bool RecalculatePath(PathRepathReason reason)
        {
            _repathCount++;
            LastRepathReason = reason;
            _nextAllowedRepathTime = Time.unscaledTime + minimumRepathInterval;

            if (PathfindingManager.TryGetScheduler(out var scheduler))
            {
                _pathVectorList.Clear();
                _currentPathIndex = 0;
                _operationWaypointCount = 0;
                _targetNextPosition = default;
                _movementDirection = Vector3.zero;
                ResetVelocitySample();
                QueuePathRequest(
                    scheduler,
                    _currentPosition,
                    isReplan: true);
                return false;
            }

            return CalculateReplannedPathSynchronously();
        }

        private bool CalculateInitialPathSynchronously(Vector3 startPosition)
        {
            var pathResult = _activePathfinder.FindPath(
                startPosition,
                RequestedDestination,
                _activePathOptions);
            CapturePathResult(pathResult);
            if (pathResult == null || !pathResult.Succeeded)
            {
                CompleteMovement(MapFailureState(LastPathStatus));
                return false;
            }

            return StartFollowingPath(pathResult.Waypoints);
        }

        private bool CalculateReplannedPathSynchronously()
        {
            var pathResult = _activePathfinder.FindPath(
                _currentPosition,
                RequestedDestination,
                _activePathOptions);
            CapturePathResult(pathResult);
            return ApplyReplannedPath(pathResult);
        }

        private bool QueuePathRequest(
            IPathRequestScheduler scheduler,
            Vector3 startPosition,
            bool isReplan)
        {
            TransitionTo(PathfinderMovementState.WaitingForPath);
            _pendingRequestIsReplan = isReplan;
            try
            {
                _pathRequestHandle = scheduler.Enqueue(
                    _activePathfinder,
                    startPosition,
                    RequestedDestination,
                    _activePathOptions,
                    requestPriority,
                    OnScheduledPathCompleted);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _pathRequestHandle = null;
                _pendingRequestIsReplan = false;
                LastPathStatus = PathStatus.InvalidConfiguration;
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }
        }

        private void OnScheduledPathCompleted(
            PathRequestHandle request,
            PathResult pathResult)
        {
            if (!ReferenceEquals(_pathRequestHandle, request))
            {
                return;
            }

            var isReplan = _pendingRequestIsReplan;
            _pathRequestHandle = null;
            _pendingRequestIsReplan = false;
            _repathPendingAfterCooldown = false;
            _pendingRepathReason = PathRepathReason.None;
            CapturePathResult(pathResult);

            if (pathResult == null || !pathResult.Succeeded)
            {
                CompleteMovement(MapFailureState(LastPathStatus));
                return;
            }

            if (isReplan)
            {
                ApplyReplannedPath(pathResult);
                return;
            }

            StartFollowingPath(pathResult.Waypoints);
        }

        private void CapturePathResult(PathResult pathResult)
        {
            _pathGridVersion = pathResult != null && pathResult.GridVersion > 0
                ? pathResult.GridVersion
                : _versionedPathfinder?.GridVersion ?? 0;
            ApplyPathResult(pathResult);
        }

        private bool ApplyReplannedPath(PathResult pathResult)
        {
            if (pathResult == null || !pathResult.Succeeded)
            {
                CompleteMovement(MapFailureState(LastPathStatus));
                return false;
            }

            if (!TryStorePath(pathResult.Waypoints, out var alreadyAtDestination))
            {
                CompleteMovement(PathfinderMovementState.Failed);
                return false;
            }

            TransitionTo(PathfinderMovementState.FollowingPath);
            var replannedOperationId = _operationId;
            PublishNotification(MovementReplanned, CreateNotification());
            if (_operationId != replannedOperationId ||
                _state != PathfinderMovementState.FollowingPath)
            {
                return false;
            }

            if (alreadyAtDestination)
            {
                CompleteMovement(PathfinderMovementState.Arrived);
                return false;
            }

            AdvancePathIfNeeded();
            return _operationId == replannedOperationId &&
                   _state == PathfinderMovementState.FollowingPath;
        }

        private bool TryStorePath(
            IReadOnlyList<Vector3> path,
            out bool alreadyAtDestination)
        {
            alreadyAtDestination = false;
            if (path == null || !HasResolvedDestination)
            {
                LastPathStatus = PathStatus.InvalidConfiguration;
                return false;
            }

            _pathVectorList.Clear();
            for (var i = 0; i < path.Count; i++)
            {
                _pathVectorList.Add(path[i]);
            }

            RefreshCurrentPosition();
            alreadyAtDestination =
                _pathVectorList.Count == 0 &&
                ((Vector2)ResolvedDestination - _currentPosition).sqrMagnitude <=
                arrivalTolerance * arrivalTolerance;

            if (!alreadyAtDestination &&
                (_pathVectorList.Count == 0 ||
                 ((Vector2)_pathVectorList[_pathVectorList.Count - 1] -
                  (Vector2)ResolvedDestination).sqrMagnitude >
                 arrivalTolerance * arrivalTolerance))
            {
                _pathVectorList.Add(ResolvedDestination);
            }

            _currentPathIndex = 0;
            _operationWaypointCount = _pathVectorList.Count;
            _targetNextPosition = _pathVectorList.Count > 0
                ? (Vector2)_pathVectorList[0]
                : default;
            _movementDirection = _pathVectorList.Count > 0
                ? (_targetNextPosition - _currentPosition).normalized
                : Vector3.zero;
            return true;
        }

        private void AdvancePathIfNeeded()
        {
            while (_state == PathfinderMovementState.FollowingPath &&
                   _currentPathIndex < _pathVectorList.Count &&
                   IsCurrentWaypointReached())
            {
                var reachedOperationId = _operationId;
                var reachedIndex = _currentPathIndex;
                var reachedWaypoint = _pathVectorList[reachedIndex];
                _currentPathIndex++;

                PublishNotification(
                    WaypointReached,
                    CreateNotification(
                        reachedIndex,
                        reachedWaypoint,
                        true));

                if (_operationId != reachedOperationId ||
                    _state == PathfinderMovementState.Cancelled ||
                    _state == PathfinderMovementState.Blocked ||
                    _state == PathfinderMovementState.Failed)
                {
                    return;
                }

                if (_currentPathIndex >= _pathVectorList.Count)
                {
                    CompleteMovement(PathfinderMovementState.Arrived);
                    return;
                }

                _targetNextPosition = _pathVectorList[_currentPathIndex];
                if (_state == PathfinderMovementState.Paused)
                {
                    _movementDirection = Vector3.zero;
                    return;
                }
            }

            if (_state == PathfinderMovementState.FollowingPath)
            {
                _movementDirection = (_targetNextPosition - _currentPosition).normalized;
            }
        }

        private void CompleteMovement(PathfinderMovementState completionState)
        {
            RefreshCurrentPosition();
            TransitionTo(completionState);
            _movementDirection = Vector3.zero;
            ResetVelocitySample();
            _currentPathIndex = 0;
            _targetNextPosition = default;
            _pathVectorList.Clear();

            var completionSource = _movementCompletionSource;
            _movementCompletionSource = null;

            var queuedRequest = _pathRequestHandle;
            _pathRequestHandle = null;
            _pendingRequestIsReplan = false;
            _repathPendingAfterCooldown = false;
            _pendingRepathReason = PathRepathReason.None;
            queuedRequest?.Cancel();

            _activePathfinder = null;
            _versionedPathfinder = null;
            _activePathOptions = null;
            var notification = CreateNotification();
            PublishTerminalNotification(completionState, notification);
            completionSource?.TrySetResult(notification);
        }

        private PathfinderMovementNotification CreateNotification(
            int waypointIndex = -1,
            Vector3 waypoint = default,
            bool hasWaypoint = false) =>
            new PathfinderMovementNotification(
                _operationId,
                _state,
                RequestedDestination,
                ResolvedDestination,
                HasResolvedDestination,
                LastPathStatus,
                LastExpandedNodeCount,
                LastPathCost,
                _currentPosition,
                _actualVelocity,
                waypointIndex,
                _operationWaypointCount,
                waypoint,
                hasWaypoint,
                _pathGridVersion,
                _repathCount,
                LastRepathReason);

        private void PublishTerminalNotification(
            PathfinderMovementState completionState,
            PathfinderMovementNotification notification)
        {
            switch (completionState)
            {
                case PathfinderMovementState.Arrived:
                    PublishNotification(MovementArrived, notification);
                    break;
                case PathfinderMovementState.Blocked:
                    PublishNotification(MovementBlocked, notification);
                    break;
                case PathfinderMovementState.Cancelled:
                    PublishNotification(MovementCancelled, notification);
                    break;
                case PathfinderMovementState.Failed:
                    PublishNotification(MovementFailed, notification);
                    break;
            }
        }

        private void PublishNotification(
            Action<PathfinderMovementNotification> handlers,
            PathfinderMovementNotification notification)
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

        private void RefreshCurrentPosition()
        {
            _currentPosition = _rigidbody != null
                ? _rigidbody.position
                : (Vector2)transform.position;
        }

        private void ResetVelocitySample()
        {
            _actualVelocity = Vector2.zero;
            if (_rigidbody == null)
            {
                _hasPhysicsPositionSample = false;
                _lastPhysicsPosition = default;
                return;
            }

            _lastPhysicsPosition = _rigidbody.position;
            _hasPhysicsPositionSample = true;
        }

        private void SampleActualVelocity(Vector2 physicsPosition)
        {
            if (!_hasPhysicsPositionSample || Time.fixedDeltaTime <= 0f)
            {
                _actualVelocity = Vector2.zero;
                _lastPhysicsPosition = physicsPosition;
                _hasPhysicsPositionSample = true;
                return;
            }

            _actualVelocity =
                (physicsPosition - _lastPhysicsPosition) / Time.fixedDeltaTime;
            _lastPhysicsPosition = physicsPosition;
        }

        private Vector2 ApplyVelocityModifiers(
            Vector2 desiredVelocity,
            float fixedDeltaTime)
        {
            var velocity = desiredVelocity;
            for (var i = 0; i < _velocityModifiers.Count; i++)
            {
                var modifier = _velocityModifiers[i];
                if (modifier == null ||
                    modifier is UnityEngine.Object unityObject && unityObject == null)
                {
                    _velocityModifiers.RemoveAt(i);
                    i--;
                    continue;
                }

                try
                {
                    var modifiedVelocity = modifier.ModifyVelocity(
                        this,
                        velocity,
                        fixedDeltaTime);
                    if (IsFinite(modifiedVelocity))
                    {
                        velocity = modifiedVelocity;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "A pathfinder movement velocity modifier returned a non-finite value.",
                            modifier as UnityEngine.Object ?? this);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(
                        exception,
                        modifier as UnityEngine.Object ?? this);
                }
            }

            return velocity;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y);

        private static int ResolveVelocityModifierOrder(
            IPathfinderMovementVelocityModifier modifier) =>
            modifier.VelocityModifierOrder;

        private bool IsCurrentWaypointReached()
        {
            var tolerance = GetToleranceForWaypoint(_currentPathIndex);
            return (_targetNextPosition - _currentPosition).sqrMagnitude <=
                   tolerance * tolerance;
        }

        private float GetToleranceForWaypoint(int waypointIndex) =>
            waypointIndex >= 0 && waypointIndex == _pathVectorList.Count - 1
                ? arrivalTolerance
                : waypointTolerance;

        private void TransitionTo(PathfinderMovementState newState)
        {
            _state = newState;
        }

        private static PathfinderMovementState MapFailureState(PathStatus status)
        {
            switch (status)
            {
                case PathStatus.StartBlocked:
                case PathStatus.DestinationBlocked:
                case PathStatus.Unreachable:
                    return PathfinderMovementState.Blocked;
                case PathStatus.Cancelled:
                    return PathfinderMovementState.Cancelled;
                default:
                    return PathfinderMovementState.Failed;
            }
        }

        private static PathRequestPriority SanitizePriority(
            PathRequestPriority priority) =>
            (PathRequestPriority)Mathf.Clamp(
                (int)priority,
                (int)PathRequestPriority.Low,
                (int)PathRequestPriority.Critical);
    }
}
