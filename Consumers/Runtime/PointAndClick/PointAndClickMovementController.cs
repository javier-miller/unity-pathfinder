using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Translates point-and-click destinations into movement requests.
    /// Input collection remains outside this component so projects can use either
    /// Unity input backend and can reject clicks consumed by UI or interactions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathfinderMovement))]
    [AddComponentMenu("Sparky Games/Pathfinder/Point And Click Movement Controller")]
    public sealed class PointAndClickMovementController : MonoBehaviour
    {
        [SerializeField]
        private PathfinderMovement movement;

        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private MovementPathOptions pathOptions = new MovementPathOptions(
            findNearestReachableDestination: true);

        /// <summary>
        /// Gets the movement component that executes this controller's requests.
        /// Its notifications expose arrival, failure, blocking and cancellation.
        /// </summary>
        public PathfinderMovement Movement => ResolveMovement();

        public Camera WorldCamera
        {
            get => worldCamera;
            set => worldCamera = value;
        }

        public MovementPathOptions PathOptions => GetPathOptions();

        private void Reset()
        {
            movement = GetComponent<PathfinderMovement>();
            worldCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveMovement();
            pathOptions ??= new MovementPathOptions(
                findNearestReachableDestination: true);
        }

        /// <summary>
        /// Converts a screen coordinate into the agent's XY movement plane and moves there.
        /// The configured camera is used, falling back to <see cref="Camera.main"/>.
        /// </summary>
        public bool MoveFromScreenPoint(Vector2 screenPosition)
        {
            var activeMovement = ResolveMovement();
            var activeCamera = worldCamera != null ? worldCamera : Camera.main;
            if (activeMovement == null || activeCamera == null)
            {
                Debug.LogWarning(
                    "Point-and-click movement requires a PathfinderMovement and a world camera.",
                    this);
                return false;
            }

            var movementPlane = new Plane(
                Vector3.forward,
                new Vector3(0f, 0f, activeMovement.transform.position.z));
            var pointerRay = activeCamera.ScreenPointToRay(screenPosition);
            if (!movementPlane.Raycast(pointerRay, out var distance))
            {
                Debug.LogWarning(
                    "The configured camera ray does not intersect the agent's XY movement plane.",
                    this);
                return false;
            }

            return MoveToWorldPoint(pointerRay.GetPoint(distance));
        }

        /// <summary>
        /// Moves to a world-space click or interaction point.
        /// The destination is projected onto the agent's current Z plane.
        /// </summary>
        public bool MoveToWorldPoint(Vector3 worldPosition)
        {
            var activeMovement = ResolveMovement();
            if (activeMovement == null)
            {
                Debug.LogWarning(
                    "Point-and-click movement requires a PathfinderMovement.",
                    this);
                return false;
            }

            worldPosition.z = activeMovement.transform.position.z;
            return activeMovement.MoveTo(
                worldPosition,
                GetPathOptions().CreateQueryOptions());
        }

        public Task<PathfinderMovementNotification> MoveToWorldPointAsync(
            Vector3 worldPosition)
        {
            var activeMovement = ResolveMovement();
            if (activeMovement == null)
            {
                throw new InvalidOperationException(
                    "Point-and-click movement requires a PathfinderMovement.");
            }

            worldPosition.z = activeMovement.transform.position.z;
            return activeMovement.MoveToAsync(
                worldPosition,
                GetPathOptions().CreateQueryOptions());
        }

        public void CancelMovement() => ResolveMovement()?.CancelMovement();

        private PathfinderMovement ResolveMovement()
        {
            if (movement == null)
            {
                movement = GetComponent<PathfinderMovement>();
            }

            return movement;
        }

        private MovementPathOptions GetPathOptions()
        {
            pathOptions ??= new MovementPathOptions(
                findNearestReachableDestination: true);
            return pathOptions;
        }
    }
}
