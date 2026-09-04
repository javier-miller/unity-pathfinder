using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Lightweight Reynolds-style separation for nearby RTS agents. It modifies
    /// movement velocity only; agents are never baked into the navigation grid.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RtsUnitMovementController))]
    [AddComponentMenu("Sparky Games/Pathfinder/RTS Local Separation")]
    public sealed class RtsLocalSeparation :
        MonoBehaviour,
        IPathfinderMovementVelocityModifier
    {
        private const float MinimumPositiveValue = 0.01f;

        private static readonly ProfilerMarker EvaluationMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.RtsLocalSeparation.Evaluate");
        private static readonly ProfilerMarker NeighborQueryMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.RtsLocalSeparation.NeighborQuery");
        private static readonly ProfilerMarker SteeringMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.RtsLocalSeparation.Steering");

        [SerializeField]
        private PathfinderMovement movement;

        [SerializeField]
        private bool usePathfinderAgentMask = true;

        [SerializeField]
        private LayerMask agentMask;

        [SerializeField]
        private bool includeTriggers;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        private float neighborRadius = 1.25f;

        [SerializeField]
        [Min(0f)]
        private float separationStrength = 4f;

        [SerializeField]
        [Range(0f, 1f)]
        private float maximumSeparationRatio = 0.75f;

        [SerializeField]
        [Range(0f, 1f)]
        private float minimumForwardRatio = 0.2f;

        [SerializeField]
        [Min(MinimumPositiveValue)]
        [Tooltip("Separation fades from full strength here to zero at arrival tolerance.")]
        private float arrivalFadeDistance = 1f;

        [SerializeField]
        [Min(0f)]
        private float responsiveness = 12f;

        [SerializeField]
        [Range(1, 128)]
        private int maximumNeighborColliders = 24;

        private Rigidbody2D _rigidbody;
        private Collider2D[] _colliderBuffer;
        private Rigidbody2D[] _uniqueBodyBuffer;
        private Vector2 _smoothedSeparationVelocity;
        private bool _timingDiagnosticsEnabled;
        private double _totalEvaluationMilliseconds;
        private double _totalNeighborQueryMilliseconds;

        public int NeighborCount { get; private set; }

        public bool WasNeighborBufferFull { get; private set; }

        public Vector2 SeparationVelocity => _smoothedSeparationVelocity;

        public float NeighborRadius => neighborRadius;

        public LayerMask EffectiveAgentMask => ResolveAgentMask();

        public bool TimingDiagnosticsEnabled => _timingDiagnosticsEnabled;

        public int VelocityModifierOrder =>
            PathfinderMovementVelocityModifierOrder.LocalAvoidance;

        public long TimingSampleCount { get; private set; }

        public double TotalEvaluationMilliseconds =>
            _totalEvaluationMilliseconds;

        public double AverageEvaluationMilliseconds =>
            TimingSampleCount > 0
                ? _totalEvaluationMilliseconds / TimingSampleCount
                : 0d;

        public double MaximumEvaluationMilliseconds { get; private set; }

        public double TotalNeighborQueryMilliseconds =>
            _totalNeighborQueryMilliseconds;

        public double AverageNeighborQueryMilliseconds =>
            TimingSampleCount > 0
                ? _totalNeighborQueryMilliseconds / TimingSampleCount
                : 0d;

        public double MaximumNeighborQueryMilliseconds { get; private set; }

        public int MaximumObservedNeighborCount { get; private set; }

        public long NeighborBufferSaturationCount { get; private set; }

        public int NeighborBufferCapacity =>
            _colliderBuffer != null
                ? _colliderBuffer.Length
                : Mathf.Clamp(maximumNeighborColliders, 1, 128);

        private void Reset()
        {
            movement = GetComponent<PathfinderMovement>();
        }

        private void Awake()
        {
            ResolveComponents();
            EnsureBuffers();
        }

        private void OnEnable()
        {
            ResolveComponents();
            EnsureBuffers();
            movement?.RegisterVelocityModifier(this);
        }

        private void OnDisable()
        {
            movement?.UnregisterVelocityModifier(this);
            ResetFrameDiagnostics();
        }

        private void Update()
        {
            if (movement == null || !movement.IsActive)
            {
                // Do not carry a stale steering impulse across pause, cancellation,
                // path scheduling or a later movement operation.
                ResetFrameDiagnostics();
            }
        }

        private void OnValidate()
        {
            neighborRadius = Mathf.Max(MinimumPositiveValue, neighborRadius);
            separationStrength = Mathf.Max(0f, separationStrength);
            maximumSeparationRatio = Mathf.Clamp01(maximumSeparationRatio);
            minimumForwardRatio = Mathf.Clamp01(minimumForwardRatio);
            arrivalFadeDistance = Mathf.Max(
                MinimumPositiveValue,
                arrivalFadeDistance);
            responsiveness = Mathf.Max(0f, responsiveness);
            maximumNeighborColliders = Mathf.Clamp(
                maximumNeighborColliders,
                1,
                128);
        }

        public Vector2 ModifyVelocity(
            PathfinderMovement activeMovement,
            Vector2 desiredVelocity,
            float fixedDeltaTime)
        {
            if (!isActiveAndEnabled ||
                activeMovement == null ||
                activeMovement != movement ||
                _rigidbody == null ||
                desiredVelocity.sqrMagnitude <= 0.000001f)
            {
                ResetFrameDiagnostics();
                return desiredVelocity;
            }

            var effectiveMask = ResolveAgentMask();
            if (effectiveMask.value == 0 ||
                neighborRadius <= 0f ||
                separationStrength <= 0f ||
                maximumSeparationRatio <= 0f)
            {
                ResetFrameDiagnostics();
                return desiredVelocity;
            }

            EnsureBuffers();
            using var evaluationMarker = EvaluationMarker.Auto();
            var evaluationStartedTimestamp = _timingDiagnosticsEnabled
                ? Stopwatch.GetTimestamp()
                : 0L;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = effectiveMask,
                useTriggers = includeTriggers
            };
            var queryStartedTimestamp = _timingDiagnosticsEnabled
                ? Stopwatch.GetTimestamp()
                : 0L;
            int colliderCount;
            using (NeighborQueryMarker.Auto())
            {
                colliderCount = Physics2D.OverlapCircle(
                    _rigidbody.position,
                    neighborRadius,
                    filter,
                    _colliderBuffer);
            }

            var queryMilliseconds = _timingDiagnosticsEnabled
                ? ElapsedMilliseconds(queryStartedTimestamp)
                : 0d;
            WasNeighborBufferFull = colliderCount >= _colliderBuffer.Length;

            var accumulated = Vector2.zero;
            var uniqueBodyCount = 0;
            NeighborCount = 0;
            Vector2 combinedVelocity;
            using (SteeringMarker.Auto())
            {
                for (var i = 0; i < colliderCount; i++)
                {
                    var neighborCollider = _colliderBuffer[i];
                    if (neighborCollider == null ||
                        neighborCollider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    var neighborBody = neighborCollider.attachedRigidbody;
                    if (neighborBody == _rigidbody ||
                        IsDuplicateBody(neighborBody, uniqueBodyCount))
                    {
                        continue;
                    }

                    if (neighborBody != null)
                    {
                        _uniqueBodyBuffer[uniqueBodyCount++] = neighborBody;
                    }

                    var neighborPosition = neighborBody != null
                        ? neighborBody.position
                        : (Vector2)neighborCollider.bounds.center;
                    var away = _rigidbody.position - neighborPosition;
                    var distance = away.magnitude;
                    if (distance >= neighborRadius)
                    {
                        continue;
                    }

                    Vector2 direction;
                    if (distance > 0.0001f)
                    {
                        direction = away / distance;
                    }
                    else
                    {
                        // Stable opposite directions let perfectly overlapping units
                        // separate instead of producing a zero vector.
                        var ownKey = _rigidbody.GetHashCode();
                        var neighborKey = neighborBody != null
                            ? neighborBody.GetHashCode()
                            : neighborCollider.GetHashCode();
                        direction = ownKey < neighborKey
                            ? Vector2.left
                            : Vector2.right;
                    }

                    var proximity = 1f - distance / neighborRadius;
                    accumulated += direction * proximity * proximity;
                    NeighborCount++;
                }

                var desiredSpeed = desiredVelocity.magnitude;
                var maximumSeparationSpeed =
                    desiredSpeed * maximumSeparationRatio;
                var targetSeparation = Vector2.ClampMagnitude(
                    accumulated * separationStrength,
                    maximumSeparationSpeed);
                targetSeparation *= GetArrivalFade(activeMovement);

                var blend = responsiveness <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-responsiveness * Mathf.Max(0f, fixedDeltaTime));
                _smoothedSeparationVelocity = Vector2.Lerp(
                    _smoothedSeparationVelocity,
                    targetSeparation,
                    blend);

                combinedVelocity = desiredVelocity +
                                   _smoothedSeparationVelocity;
                var forward = desiredVelocity / desiredSpeed;
                var minimumForwardSpeed = desiredSpeed * minimumForwardRatio;
                var currentForwardSpeed = Vector2.Dot(combinedVelocity, forward);
                if (currentForwardSpeed < minimumForwardSpeed)
                {
                    combinedVelocity += forward *
                                        (minimumForwardSpeed - currentForwardSpeed);
                }

            }

            RecordTimingDiagnostics(
                evaluationStartedTimestamp,
                queryMilliseconds);
            return combinedVelocity;
        }

        /// <summary>
        /// Enables the opt-in Stopwatch counters used by runtime diagnostics.
        /// Unity Profiler markers remain available regardless of this setting.
        /// </summary>
        public void SetTimingDiagnosticsEnabled(
            bool enabled,
            bool resetCounters = true)
        {
            _timingDiagnosticsEnabled = enabled;
            if (resetCounters)
            {
                ResetTimingDiagnostics();
            }
        }

        public void ResetTimingDiagnostics()
        {
            TimingSampleCount = 0;
            _totalEvaluationMilliseconds = 0d;
            MaximumEvaluationMilliseconds = 0d;
            _totalNeighborQueryMilliseconds = 0d;
            MaximumNeighborQueryMilliseconds = 0d;
            MaximumObservedNeighborCount = 0;
            NeighborBufferSaturationCount = 0;
        }

        private void RecordTimingDiagnostics(
            long evaluationStartedTimestamp,
            double queryMilliseconds)
        {
            if (!_timingDiagnosticsEnabled)
            {
                return;
            }

            var evaluationMilliseconds = ElapsedMilliseconds(
                evaluationStartedTimestamp);
            TimingSampleCount++;
            _totalEvaluationMilliseconds += evaluationMilliseconds;
            MaximumEvaluationMilliseconds = Math.Max(
                MaximumEvaluationMilliseconds,
                evaluationMilliseconds);
            _totalNeighborQueryMilliseconds += queryMilliseconds;
            MaximumNeighborQueryMilliseconds = Math.Max(
                MaximumNeighborQueryMilliseconds,
                queryMilliseconds);
            MaximumObservedNeighborCount = Math.Max(
                MaximumObservedNeighborCount,
                NeighborCount);
            if (WasNeighborBufferFull)
            {
                NeighborBufferSaturationCount++;
            }
        }

        private static double ElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) *
            1000d / Stopwatch.Frequency;

        private float GetArrivalFade(PathfinderMovement activeMovement)
        {
            if (!activeMovement.HasResolvedDestination)
            {
                return 1f;
            }

            var distance = Vector2.Distance(
                _rigidbody.position,
                activeMovement.ResolvedDestination);
            var zeroDistance = activeMovement.ArrivalTolerance;
            var fullDistance = Mathf.Max(
                arrivalFadeDistance,
                zeroDistance + MinimumPositiveValue);
            return Mathf.InverseLerp(zeroDistance, fullDistance, distance);
        }

        private bool IsDuplicateBody(Rigidbody2D body, int bodyCount)
        {
            if (body == null)
            {
                return false;
            }

            for (var i = 0; i < bodyCount; i++)
            {
                if (_uniqueBodyBuffer[i] == body)
                {
                    return true;
                }
            }

            return false;
        }

        private LayerMask ResolveAgentMask()
        {
            if (usePathfinderAgentMask &&
                PathfindingManager.GetInstance() is Pathfinding pathfinding)
            {
                return pathfinding.AgentMask;
            }

            return agentMask;
        }

        private void ResolveComponents()
        {
            var localMovement = GetComponent<PathfinderMovement>();
            if (movement != localMovement)
            {
                movement = localMovement;
            }

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody2D>();
            }
        }

        private void EnsureBuffers()
        {
            var capacity = Mathf.Clamp(maximumNeighborColliders, 1, 128);
            if (_colliderBuffer == null || _colliderBuffer.Length != capacity)
            {
                _colliderBuffer = new Collider2D[capacity];
                _uniqueBodyBuffer = new Rigidbody2D[capacity];
            }
        }

        private void ResetFrameDiagnostics()
        {
            NeighborCount = 0;
            WasNeighborBufferFull = false;
            _smoothedSeparationVelocity = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, neighborRadius);

            if (_smoothedSeparationVelocity.sqrMagnitude > 0.000001f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(
                    transform.position,
                    transform.position +
                    (Vector3)_smoothedSeparationVelocity * 0.25f);
            }

            Gizmos.color = previousColor;
        }
    }
}
