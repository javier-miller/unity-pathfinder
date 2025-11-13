using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder.Editor
{
    /// <summary>
    /// Pathfinding Rectangle Editor
    /// </summary>
    /// <seealso cref="UnityEditor.Editor" />
    [CustomEditor(typeof(PathfindingTilemap))]
    public class PathfindingTileEditor : UnityEditor.Editor
    {
        private PathfindingTilemap _pathfinding;
        private Vector3 lastPosition;

        /// <summary>
        /// Called when [enable].
        /// </summary>
        private void OnEnable()
        {
            _pathfinding = (PathfindingTilemap)target;
            lastPosition = _pathfinding.transform.position;
        }

        /// <summary>
        /// Implement this function to make a custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (_pathfinding.transform.position != lastPosition)
            {
                lastPosition = _pathfinding.transform.position;
                _pathfinding.Refresh();
            }
        }
    }
}