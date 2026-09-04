using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Optional vehicle movement profile. Infantry remains omnidirectional while
    /// vehicles rotate their velocity at a bounded angular rate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(RtsUnitMovementController))]
    [AddComponentMenu("Sparky Games/Pathfinder/RTS Vehicle Steering")]
    public sealed class RtsVehicleSteering :
        MonoBehaviour,
        IPathfinderMovementVelocityModifier
    {
        [SerializeField]
        private PathfinderMovement movement;

        [SerializeField]
        [Min(1f)]
        private float maximumTurnDegreesPerSecond = 180f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Retained speed while facing directly away from the requested velocity.")]
        private float minimumTurningSpeedRatio = 0.2f;

        [SerializeField]
        private bool orientTransform = true;

        [SerializeField]
        private RtsVehicleForwardAxis forwardAxis = RtsVehicleForwardAxis.Right;

        private Rigidbody2D _rigidbody;

        public float MaximumTurnDegreesPerSecond => maximumTurnDegreesPerSecond;

        public Vector2 Forward => GetForward();

        public int VelocityModifierOrder =>
            PathfinderMovementVelocityModifierOrder.LocomotionConstraint;

        private void Reset() => movement = GetComponent<PathfinderMovement>();

        private void Awake() => ResolveComponents();

        private void OnEnable()
        {
            ResolveComponents();
            movement?.RegisterVelocityModifier(this);
        }

        private void OnDisable()
        {
            movement?.UnregisterVelocityModifier(this);
        }

        private void OnValidate()
        {
            maximumTurnDegreesPerSecond = Mathf.Max(
                1f,
                maximumTurnDegreesPerSecond);
            minimumTurningSpeedRatio = Mathf.Clamp01(minimumTurningSpeedRatio);
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
                return desiredVelocity;
            }

            var desiredSpeed = desiredVelocity.magnitude;
            var desiredDirection = desiredVelocity / desiredSpeed;
            var currentForward = GetForward();
            var signedAngle = Vector2.SignedAngle(
                currentForward,
                desiredDirection);
            var maximumStep = maximumTurnDegreesPerSecond * fixedDeltaTime;
            var appliedAngle = Mathf.Clamp(
                signedAngle,
                -maximumStep,
                maximumStep);
            var newForward = Rotate(currentForward, appliedAngle).normalized;
            var turnRatio = Mathf.Clamp01(Mathf.Abs(signedAngle) / 180f);
            var speedRatio = Mathf.Lerp(
                1f,
                minimumTurningSpeedRatio,
                turnRatio);

            if (orientTransform)
            {
                var worldAngle = Mathf.Atan2(newForward.y, newForward.x) *
                                 Mathf.Rad2Deg;
                if (forwardAxis == RtsVehicleForwardAxis.Up)
                {
                    worldAngle -= 90f;
                }

                _rigidbody.MoveRotation(worldAngle);
            }

            return newForward * desiredSpeed * speedRatio;
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

        private Vector2 GetForward()
        {
            if (_rigidbody == null)
            {
                return forwardAxis == RtsVehicleForwardAxis.Up
                    ? (Vector2)transform.up
                    : (Vector2)transform.right;
            }

            var angle = _rigidbody.rotation * Mathf.Deg2Rad;
            if (forwardAxis == RtsVehicleForwardAxis.Up)
            {
                angle += Mathf.PI * 0.5f;
            }

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var sine = Mathf.Sin(radians);
            var cosine = Mathf.Cos(radians);
            return new Vector2(
                vector.x * cosine - vector.y * sine,
                vector.x * sine + vector.y * cosine);
        }
    }
}
