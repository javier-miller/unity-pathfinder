using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Tilemap
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    [RequireComponent(typeof(Tilemap))]
    [ExecuteInEditMode]
    public class PathfindingTilemap : Pathfinding
    {
        private Tilemap _tilemap;

        /// <summary>
        /// Awakes this instance.
        /// </summary>
        protected override void Awake()
        {
            _tilemap = GetComponent<Tilemap>();

            base.Awake();
        }

        /// <summary>
        /// Builds the grid.
        /// </summary>
        /// <returns></returns>
        protected override Grid BuildGrid()
        {
            var bounds = _tilemap.cellBounds;
            var gridWidth = bounds.size.x;
            var gridHeight = bounds.size.y;

            var gridPosition = new Vector3((gridWidth / -2) + transform.position.x, (gridHeight / -2) + transform.position.y);
            var result = new Grid(new Vector2Int(gridWidth, gridHeight), _tilemap.cellSize, gridPosition);

            var colliderCellSize = _tilemap.cellSize - new Vector3(0.1f, 0.1f);

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    var cellPosition = new Vector3Int(x + bounds.xMin, y + bounds.yMin, 0);
                    var worldPosition = _tilemap.GetCellCenterWorld(cellPosition);
                    var checknoWalkable = Physics2D.OverlapBox(worldPosition, colliderCellSize, 0, layerMask: colliderMask);
                    result.AddCell(new Vector2Int(x, y), worldPosition, checknoWalkable is null);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the cell position.
        /// </summary>
        /// <param name="worldPosition">The world position.</param>
        /// <returns></returns>
        protected override Vector3Int GetCellPosition(Vector3 worldPosition) =>
            _tilemap.WorldToCell(worldPosition) - _tilemap.cellBounds.min;

#if UNITY_EDITOR
        /// <summary>
        /// Called when [validate].
        /// </summary>
        private void OnValidate()
        {
            if (_tilemap is null) _tilemap = GetComponent<Tilemap>();

            Refresh();
            // Este método se llama automáticamente cuando cambias un valor en el inspector.
            // Asegura que el Gizmo se actualice cuando cambies las propiedades en el Inspector.
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
