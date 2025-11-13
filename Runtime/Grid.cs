using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Grid
    /// </summary>
    public class Grid
    {
        private readonly Vector2Int _cellsCount;
        private readonly Vector2 _gridSize;
        private readonly Vector2 _cellSize;
        private readonly GridCell[,] _cells;
        private readonly List<GridCell> _itemCollection;
        private readonly Vector3 _originPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grid"/> class.
        /// </summary>
        /// <param name="cellsCount">The cells count.</param>
        /// <param name="cellSize">Size of the cell.</param>
        /// <param name="originPosition">The origin position.</param>
        public Grid(Vector2Int cellsCount, Vector2 cellSize, Vector3 originPosition)
        {
            _originPosition = originPosition;
            _cellsCount = cellsCount;   
            _cellSize = cellSize;
            _gridSize = cellSize * cellsCount;

            _cells = new GridCell[cellsCount.x, cellsCount.y];
            _itemCollection = new List<GridCell>();
        }

        /// <summary>
        /// Gets the size.
        /// </summary>
        /// <value>
        /// The size.
        /// </value>
        public Vector2 Size => _gridSize;

        /// <summary>
        /// Gets the size of the cell.
        /// </summary>
        /// <value>
        /// The size of the cell.
        /// </value>
        public Vector3 CellSize => _cellSize;

        /// <summary>
        /// Gets the cells count.
        /// </summary>
        /// <value>
        /// The cells count.
        /// </value>
        public Vector2Int CellsCount => _cellsCount;

        /// <summary>
        /// Adds the cell.
        /// </summary>
        /// <param name="coordenates">The coordenates.</param>
        /// <param name="worldPosition">The world position.</param>
        /// <param name="isWalkable">if set to <c>true</c> [is walkable].</param>
        public void AddCell(Vector2Int coordenates, Vector3 worldPosition, bool isWalkable = true)
        {
            var cell = new GridCell(coordenates, worldPosition);
            _cells[coordenates.x, coordenates.y] = cell;
            cell.IsWalkable = isWalkable;
            _itemCollection.Add(cell);
        }

        /// <summary>
        /// Gets all cells.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GridCell> GetAllCells() => _itemCollection.ToArray();

        /// <summary>
        /// Gets the cell.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns></returns>
        public GridCell GetCell(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < _cellsCount.x && y < _cellsCount.y) return _cells[x, y];
            else return default;
        }

        /// <summary>
        /// Creates the path node.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns></returns>
        public PathNode CreatePathNode(int x, int y)
        {
            var cell = GetCell(x, y);
            return cell.CreatePathNode();
        }
    }
}
