using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Movement-order boundary for one RTS unit. Selection, formation-slot assignment,
    /// command queues and local avoidance intentionally remain outside this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathfinderMovement))]
    [AddComponentMenu("Sparky Games/Pathfinder/RTS Unit Movement Controller")]
    public sealed class RtsUnitMovementController : MonoBehaviour
    {
        [SerializeField]
        private PathfinderMovement movement;

        [SerializeField]
        private MovementPathOptions pathOptions = new MovementPathOptions(
            findNearestReachableDestination: true);

        /// <summary>
        /// Gets the underlying state machine. Subscribe to its detailed notifications
        /// to react to arrival, failure, blocking or cancellation.
        /// </summary>
        public PathfinderMovement Movement => ResolveMovement();

        public MovementPathOptions PathOptions => GetPathOptions();

        public bool HasPendingOrder =>
            ResolveMovement() != null && movement.HasPendingMovement;

        private void Reset() => movement = GetComponent<PathfinderMovement>();

        private void Awake()
        {
            ResolveMovement();
            pathOptions ??= new MovementPathOptions(
                findNearestReachableDestination: true);
        }

        /// <summary>
        /// Replaces any active or paused movement with a new world-space order.
        /// </summary>
        public bool IssueMoveOrder(Vector3 worldDestination)
        {
            var activeMovement = ResolveMovement();
            if (activeMovement == null)
            {
                Debug.LogWarning(
                    "An RTS unit movement controller requires PathfinderMovement.",
                    this);
                return false;
            }

            worldDestination.z = activeMovement.transform.position.z;
            return activeMovement.MoveTo(
                worldDestination,
                GetPathOptions().CreateQueryOptions());
        }

        public Task<PathfinderMovementNotification> IssueMoveOrderAsync(
            Vector3 worldDestination)
        {
            var activeMovement = ResolveMovement();
            if (activeMovement == null)
            {
                throw new InvalidOperationException(
                    "An RTS unit movement controller requires PathfinderMovement.");
            }

            worldDestination.z = activeMovement.transform.position.z;
            return activeMovement.MoveToAsync(
                worldDestination,
                GetPathOptions().CreateQueryOptions());
        }

        public void CancelCurrentOrder() => ResolveMovement()?.CancelMovement();

        public bool PauseCurrentOrder() =>
            ResolveMovement() != null && movement.PauseMovement();

        public bool ResumeCurrentOrder() =>
            ResolveMovement() != null && movement.ResumeMovement();

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
