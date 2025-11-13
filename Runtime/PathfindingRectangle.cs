using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Rectangle
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    [ExecuteInEditMode]
    public class PathfindingRectangle : Pathfinding
    {
        private Vector2 _offset = Vector2.zero;

        [SerializeField]
        private Vector2 gridSize = new Vector2(100, 100);

        [SerializeField]
        private Vector2 tileSize = new Vector2(10, 10);

        protected override void Awake()
        {
            base.Awake();
            CalculateOffset();
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();
            CalculateOffset();
        }

        /// <summary>
        /// Builds the grid.
        /// </summary>
        /// <returns></returns>
        protected override Grid BuildGrid()
        {
            var numCellsX = Mathf.FloorToInt(gridSize.x / tileSize.x);
            var numCellsY = Mathf.FloorToInt(gridSize.y / tileSize.y);

            var gridPosition = new Vector3((numCellsX / -2) + transform.position.x, (numCellsY / -2) + transform.position.y);
            var result = new Grid(new Vector2Int(numCellsX, numCellsY), tileSize, gridPosition);

            var colliderCellSize = tileSize - new Vector2(0.1f, 0.1f);

            for (int x = 0; x < numCellsX; x++)
            {
                for (int y = 0; y < numCellsY; y++)
                {
                    var cellPosition = new Vector3Int(x, y, 0);

                    var xPos = transform.position.x + x * tileSize.x;
                    var yPos = transform.position.y + y * tileSize.y;

                    var cellCoordenates = new Vector2Int(x, y);
                    var worldPosition = GetWorldPosition2(cellCoordenates, tileSize, numCellsX, numCellsY);
                    var checknoWalkable = Physics2D.OverlapBox(worldPosition, colliderCellSize, 0, layerMask: colliderMask);
                    result.AddCell(cellCoordenates, worldPosition, checknoWalkable is null);
                }
            }

            return result;
        }

        private Vector2 GetWorldPosition(Vector2Int gridPosition, Vector2 tileSize) =>
                transform.position + new Vector3(gridPosition.x * tileSize.x, gridPosition.y * tileSize.y, 0);

        private Vector3 GetWorldPosition2(Vector2Int gridPosition, Vector2 tileSize, int numCellsX, int numCellsY)
        {
            // Calcula el desplazamiento para centrar el grid
            float offsetX = (numCellsX - 1) * tileSize.x * 0.5f;
            float offsetY = (numCellsY - 1) * tileSize.y * 0.5f;

            // Calcula la posición en el mundo según las coordenadas del grid, ajustando con la posición de origen
            return transform.position + new Vector3(gridPosition.x * tileSize.x - offsetX, gridPosition.y * tileSize.y - offsetY, 0);
        }

        /// <summary>
        /// Calculates the offset.
        /// </summary>
        private void CalculateOffset()
        {
            float offsetX = (_grid.CellsCount.x - 1) * _grid.CellSize.x * 0.5f;
            float offsetY = (_grid.CellsCount.y - 1) * _grid.CellSize.y * 0.5f;

            _offset = new Vector2(offsetX, offsetY);
        }

        /// <summary>
        /// Gets the cell position.
        /// </summary>
        /// <param name="worldPosition">The world position.</param>
        /// <returns></returns>
        protected override Vector3Int GetCellPosition(Vector3 worldPosition)
        {
            // Ajusta la posición en el mundo restando la posición de origen
            var adjustedWorldPosition = worldPosition - (transform.position - (_grid.CellSize / 2));

            int x = Mathf.FloorToInt((adjustedWorldPosition.x + _offset.x) / _grid.CellSize.x);
            int y = Mathf.FloorToInt((adjustedWorldPosition.y + _offset.y) / _grid.CellSize.y);

            return new Vector3Int(x, y);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Called when [validate].
        /// </summary>
        private void OnValidate()
        {
            Refresh();
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
