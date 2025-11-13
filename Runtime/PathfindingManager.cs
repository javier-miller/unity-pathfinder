using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Manager
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    public class PathfindingManager : MonoBehaviour, IPathfinding
    {
        private static PathfindingManager _instance;

        [SerializeField]
        private Pathfinding pathfindingSelected;

        /// <summary>
        /// Awakes this instance.
        /// </summary>
        private void Awake()
        {
            OnCreateInstance();
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <returns></returns>
        public static IPathfinding GetInstance() => _instance.pathfindingSelected;

        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        public bool GetPath(Vector3 position, out Vector3 result)
        {
            result = default;
            if (pathfindingSelected is null) return false;

            return pathfindingSelected.GetPath(position, out result);
        }

        /// <summary>
        /// Finds the path.
        /// </summary>
        /// <param name="startWorldPosition">The start world position.</param>
        /// <param name="endWorldPosition">The end world position.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public bool FindPath(Vector3 startWorldPosition, Vector3 endWorldPosition, out IEnumerable<Vector3> path)
        {
            path = Enumerable.Empty<Vector3>();
            if (pathfindingSelected is null) return false;

            return pathfindingSelected.FindPath(startWorldPosition, endWorldPosition, out path);
        }

        /// <summary>
        /// Called when [create instance].
        /// </summary>
        private void OnCreateInstance()
        {
            if (_instance == null) _instance = this;
            else if (_instance != this) Destroy(gameObject);
        }
    }
}
