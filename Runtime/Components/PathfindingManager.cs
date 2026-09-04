using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Publishes the active pathfinder and central request scheduler.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathRequestScheduler))]
    public class PathfindingManager : MonoBehaviour, IPathfinding
    {
        private static PathfindingManager _instance;

        [SerializeField]
        private Pathfinding pathfindingSelected;

        [SerializeField]
        private PathRequestScheduler pathRequestScheduler;

        private void Awake()
        {
            OnCreateInstance();
            if (_instance == this)
            {
                ResolveScheduler();
            }
        }

        /// <summary>
        /// Gets the active pathfinder, or null while the manager is not ready.
        /// </summary>
        public static IPathfinding GetInstance()
        {
            if (_instance == null || _instance.pathfindingSelected == null)
            {
                return null;
            }

            return _instance.pathfindingSelected;
        }

        /// <summary>
        /// Tries to get the active pathfinder.
        /// </summary>
        public static bool TryGetInstance(out IPathfinding pathfinding)
        {
            pathfinding = GetInstance();
            return pathfinding != null;
        }

        /// <summary>
        /// Gets the active central scheduler, or null while it is unavailable.
        /// </summary>
        public static IPathRequestScheduler GetScheduler()
        {
            if (_instance == null)
            {
                return null;
            }

            var scheduler = _instance.ResolveScheduler();
            return scheduler != null && scheduler.isActiveAndEnabled
                ? scheduler
                : null;
        }

        /// <summary>
        /// Tries to get the active central request scheduler.
        /// </summary>
        public static bool TryGetScheduler(out IPathRequestScheduler scheduler)
        {
            scheduler = GetScheduler();
            return scheduler != null;
        }

        public bool TryGetWalkablePosition(Vector3 position, out Vector3 result)
        {
            result = default;
            return pathfindingSelected != null &&
                   pathfindingSelected.TryGetWalkablePosition(position, out result);
        }

        public PathResult FindPath(
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            PathQueryOptions options)
        {
            if (pathfindingSelected == null)
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    endWorldPosition);
            }

            return pathfindingSelected.FindPath(
                startWorldPosition,
                endWorldPosition,
                options);
        }

        public void SetActivePathfinder(Pathfinding pathfinder)
        {
            pathfindingSelected = pathfinder;
        }

        private PathRequestScheduler ResolveScheduler()
        {
            if (pathRequestScheduler == null)
            {
                TryGetComponent(out pathRequestScheduler);
            }

            if (pathRequestScheduler == null && Application.isPlaying)
            {
                pathRequestScheduler = gameObject.AddComponent<PathRequestScheduler>();
            }

            return pathRequestScheduler;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnCreateInstance()
        {
            if (_instance == null)
            {
                _instance = this;
                return;
            }

            if (_instance != this)
            {
                Debug.LogWarning("A duplicate PathfindingManager was removed.", this);
                Destroy(this);
            }
        }
    }
}
