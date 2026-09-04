using System;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Detects repeated low-progress windows while a unit is expected to move.
    /// Recovery requests are bounded and pass through PathfinderMovement's cooldown.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(RtsUnitMovementController))]
    [AddComponentMenu("Sparky Games/Pathfinder/RTS Stuck Detector")]
    public sealed class RtsStuckDetector : MonoBehaviour
    {
        private const float MinimumPositiveValue = 0.01f;

        [SerializeField]
        private PathfinderMovement movement;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        private float observationWindow = 0.75f;

        [SerializeField]
        [Min(0f)]
        private float minimumProgressDistance = 0.15f;

        [SerializeField]
        [Min(1)]
        private int requiredConsecutiveWindows = 2;

        [SerializeField]
        private bool automaticallyRequestRepath = true;

        [SerializeField]
        [Min(0)]
        private int maximumRecoveryAttempts = 3;

        private Rigidbody2D _rigidbody;
        private Vector2 _windowStartPosition;
        private float _windowStartTime;
        private int _observedOperationId;
        private int _consecutiveStuckWindows;
        private int _recoveryAttemptCount;
        private bool _hasObservation;
        private bool _exhaustionPublished;

        public event Action<RtsStuckNotification> StuckDetected;

        public event Action<RtsStuckNotification> RecoveryExhausted;

        public int ConsecutiveStuckWindows => _consecutiveStuckWindows;

        public int RecoveryAttemptCount => _recoveryAttemptCount;

        public float ObservationWindow => observationWindow;

        public float MinimumProgressDistance => minimumProgressDistance;

        private void Reset()
        {
            movement = GetComponent<PathfinderMovement>();
        }

        private void Awake()
        {
            ResolveComponents();
            ResetForOperation();
        }

        private void OnEnable()
        {
            ResolveComponents();
            ResetForOperation();
        }

        private void OnDisable() => ClearObservation();

        private void OnValidate()
        {
            observationWindow = Mathf.Max(MinimumPositiveValue, observationWindow);
            minimumProgressDistance = Mathf.Max(0f, minimumProgressDistance);
            requiredConsecutiveWindows = Mathf.Max(1, requiredConsecutiveWindows);
            maximumRecoveryAttempts = Mathf.Max(0, maximumRecoveryAttempts);
        }

        private void FixedUpdate()
        {
            if (movement == null || _rigidbody == null)
            {
                ResolveComponents();
                return;
            }

            if (_observedOperationId != movement.OperationId)
            {
                ResetForOperation();
            }

            if (!ShouldObserveMovement())
            {
                ClearObservation();
                return;
            }

            if (!_hasObservation)
            {
                StartObservation();
                return;
            }

            if (Time.unscaledTime - _windowStartTime < observationWindow)
            {
                return;
            }

            EvaluateWindow();
        }

        private bool ShouldObserveMovement()
        {
            if (movement.State != PathfinderMovementState.FollowingPath ||
                movement.MovementDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            if (!movement.HasResolvedDestination)
            {
                return true;
            }

            var remaining =
                (Vector2)movement.ResolvedDestination - _rigidbody.position;
            return remaining.sqrMagnitude >
                   movement.ArrivalTolerance * movement.ArrivalTolerance;
        }

        private void EvaluateWindow()
        {
            var currentPosition = _rigidbody.position;
            var progress = Vector2.Distance(_windowStartPosition, currentPosition);
            _windowStartPosition = currentPosition;
            _windowStartTime = Time.unscaledTime;

            if (progress >= minimumProgressDistance)
            {
                _consecutiveStuckWindows = 0;
                _exhaustionPublished = false;
                return;
            }

            // Once recovery is exhausted, keep observing progress but do not emit
            // the same terminal stuck episode every observation window. Movement
            // or a new operation rearms the detector through the branches above.
            if (_exhaustionPublished)
            {
                return;
            }

            _consecutiveStuckWindows++;
            if (_consecutiveStuckWindows < requiredConsecutiveWindows)
            {
                return;
            }

            var repathAccepted = false;
            if (automaticallyRequestRepath &&
                _recoveryAttemptCount < maximumRecoveryAttempts)
            {
                repathAccepted = movement.RequestRepath(
                    PathRepathReason.StuckRecovery);
                if (repathAccepted)
                {
                    _recoveryAttemptCount++;
                    _consecutiveStuckWindows = 0;
                }
            }

            var exhausted = automaticallyRequestRepath &&
                            _recoveryAttemptCount >= maximumRecoveryAttempts &&
                            !repathAccepted;
            var notification = new RtsStuckNotification(
                movement.OperationId,
                currentPosition,
                progress,
                _consecutiveStuckWindows,
                _recoveryAttemptCount,
                repathAccepted,
                exhausted);
            Publish(StuckDetected, notification);

            if (exhausted && !_exhaustionPublished)
            {
                _exhaustionPublished = true;
                Publish(RecoveryExhausted, notification);
            }
        }

        private void ResolveComponents()
        {
            if (movement == null)
            {
                movement = GetComponent<PathfinderMovement>();
            }

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody2D>();
            }
        }

        private void ResetForOperation()
        {
            _observedOperationId = movement != null ? movement.OperationId : 0;
            _consecutiveStuckWindows = 0;
            _recoveryAttemptCount = 0;
            _exhaustionPublished = false;
            ClearObservation();
        }

        private void StartObservation()
        {
            _windowStartPosition = _rigidbody.position;
            _windowStartTime = Time.unscaledTime;
            _hasObservation = true;
        }

        private void ClearObservation()
        {
            _hasObservation = false;
            _windowStartPosition = default;
            _windowStartTime = 0f;
        }

        private void Publish(
            Action<RtsStuckNotification> handlers,
            RtsStuckNotification notification)
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
    }
}
