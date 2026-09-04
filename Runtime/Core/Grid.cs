using System;
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
        private long _version = 1;
        private int _nonDefaultTraversalCostCellCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grid"/> class.
        /// </summary>
        /// <param name="cellsCount">The cells count.</param>
        /// <param name="cellSize">Size of the cell.</param>
        public Grid(Vector2Int cellsCount, Vector2 cellSize)
        {
            if (cellsCount.x < 0 || cellsCount.y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsCount), "Cell counts cannot be negative.");
            }

            if (cellSize.x <= 0f || cellSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive on both axes.");
            }

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
        /// Gets the total number of addressable cells.
        /// </summary>
        public int CellCount => _cellsCount.x * _cellsCount.y;

        /// <summary>
        /// Gets the revision of the navigation-data snapshot. A complete rebuild or a
        /// partial update that changes at least one cell advances this value once.
        /// </summary>
        public long Version => _version;

        /// <summary>
        /// Gets the version assigned when this complete grid snapshot was published.
        /// A retained route older than this value belongs to different geometry.
        /// </summary>
        public long SnapshotVersion { get; private set; } = 1;

        /// <summary>
        /// Gets the most recent version that changed at least one traversal cost.
        /// </summary>
        public long LastTraversalCostChangeVersion { get; private set; } = 1;

        /// <summary>
        /// Gets whether at least one cell currently uses a non-default terrain cost.
        /// </summary>
        public bool HasWeightedTerrain =>
            _nonDefaultTraversalCostCellCount > 0;

        /// <summary>
        /// Checks a conservative rectangular clearance around one cell for a circular
        /// agent radius. Cells outside the grid count as blocked.
        /// </summary>
        public bool HasClearance(Vector2Int coordinates, float agentRadius)
        {
            if (float.IsNaN(agentRadius) ||
                float.IsInfinity(agentRadius) ||
                agentRadius < 0f)
            {
                return false;
            }

            var centerCell = GetCell(coordinates.x, coordinates.y);
            if (centerCell == null || !centerCell.IsWalkable)
            {
                return false;
            }

            if (agentRadius <= _cellSize.x * 0.5f &&
                agentRadius <= _cellSize.y * 0.5f)
            {
                return true;
            }

            var horizontalRings = Mathf.Max(
                0,
                Mathf.CeilToInt(
                    (agentRadius - _cellSize.x * 0.5f) / _cellSize.x));
            var verticalRings = Mathf.Max(
                0,
                Mathf.CeilToInt(
                    (agentRadius - _cellSize.y * 0.5f) / _cellSize.y));

            for (var x = coordinates.x - horizontalRings;
                 x <= coordinates.x + horizontalRings;
                 x++)
            {
                for (var y = coordinates.y - verticalRings;
                     y <= coordinates.y + verticalRings;
                     y++)
                {
                    var cell = GetCell(x, y);
                    if (cell == null || !cell.IsWalkable)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Adds the cell.
        /// </summary>
        /// <param name="coordinates">The coordinates.</param>
        /// <param name="worldPosition">The world position.</param>
        /// <param name="isWalkable">if set to <c>true</c> [is walkable].</param>
        public void AddCell(
            Vector2Int coordinates,
            Vector3 worldPosition,
            bool isWalkable = true,
            int traversalCost = GridCell.DefaultTraversalCost)
        {
            if (!Contains(coordinates.x, coordinates.y))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinates), "Cell coordinates are outside the grid.");
            }

            if (_cells[coordinates.x, coordinates.y] != null)
            {
                throw new InvalidOperationException("A cell already exists at the requested coordinates.");
            }

            var cell = new GridCell(
                coordinates,
                worldPosition,
                this,
                isWalkable,
                traversalCost,
                _version);
            _cells[coordinates.x, coordinates.y] = cell;
            _itemCollection.Add(cell);
            if (traversalCost != GridCell.DefaultTraversalCost)
            {
                _nonDefaultTraversalCostCellCount++;
            }
        }

        /// <summary>
        /// Gets all cells.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GridCell> GetAllCells() => _itemCollection;

        /// <summary>
        /// Gets the cell.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns></returns>
        public GridCell GetCell(int x, int y)
        {
            return Contains(x, y) ? _cells[x, y] : null;
        }

        internal bool Contains(int x, int y) =>
            x >= 0 && y >= 0 && x < _cellsCount.x && y < _cellsCount.y;

        internal int GetIndex(int x, int y) => y * _cellsCount.x + x;

        internal GridCell GetCellByIndex(int index)
        {
            if (index < 0 || index >= CellCount || _cellsCount.x == 0)
            {
                return null;
            }

            var x = index % _cellsCount.x;
            var y = index / _cellsCount.x;
            return _cells[x, y];
        }

        internal void InitializeVersion(long version)
        {
            if (version < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(version),
                    "Grid version must be positive.");
            }

            _version = version;
            SnapshotVersion = version;
            LastTraversalCostChangeVersion = version;
            for (var i = 0; i < _itemCollection.Count; i++)
            {
                _itemCollection[i].SetNavigationDataChangedVersion(version);
            }
        }

        /// <summary>
        /// Applies one atomic navigation-data batch. The grid version advances once
        /// when at least one cell changes and remains unchanged for a no-op batch.
        /// </summary>
        public GridNavigationChangeSummary ApplyNavigationUpdates(
            IReadOnlyList<GridCellNavigationUpdate> updates)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (updates.Count == 0)
            {
                return default;
            }

            var hasChanges = false;
            for (var i = 0; i < updates.Count; i++)
            {
                var update = updates[i];
                var cell = GetCell(update.Coordinates.x, update.Coordinates.y);
                if (cell == null)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(updates),
                        $"Cell {update.Coordinates} is outside the populated grid.");
                }

                hasChanges |= cell.IsWalkable != update.IsWalkable ||
                    cell.TraversalCost != update.TraversalCost;
            }

            if (!hasChanges)
            {
                return default;
            }

            var nextVersion = GetNextVersion();
            var changedCount = 0;
            var changedWalkabilityCount = 0;
            var changedTraversalCostCount = 0;
            for (var i = 0; i < updates.Count; i++)
            {
                var update = updates[i];
                var cell = GetCell(update.Coordinates.x, update.Coordinates.y);
                var walkabilityChanged = cell.IsWalkable != update.IsWalkable;
                var traversalCostChanged =
                    cell.TraversalCost != update.TraversalCost;
                if (!walkabilityChanged && !traversalCostChanged)
                {
                    continue;
                }

                if (traversalCostChanged)
                {
                    _nonDefaultTraversalCostCellCount +=
                        update.TraversalCost == GridCell.DefaultTraversalCost ? -1 : 0;
                    _nonDefaultTraversalCostCellCount +=
                        cell.TraversalCost == GridCell.DefaultTraversalCost ? 1 : 0;
                }

                cell.SetNavigationData(
                    update.IsWalkable,
                    update.TraversalCost,
                    nextVersion);
                changedCount++;
                changedWalkabilityCount += walkabilityChanged ? 1 : 0;
                changedTraversalCostCount += traversalCostChanged ? 1 : 0;
            }

            if (changedCount > 0)
            {
                _version = nextVersion;
                if (changedTraversalCostCount > 0)
                {
                    LastTraversalCostChangeVersion = nextVersion;
                }
            }

            return new GridNavigationChangeSummary(
                changedCount,
                changedWalkabilityCount,
                changedTraversalCostCount);
        }

        internal void SetCellWalkability(GridCell cell, bool isWalkable)
        {
            if (cell == null || cell.IsWalkable == isWalkable)
            {
                return;
            }

            var nextVersion = GetNextVersion();
            cell.SetNavigationData(
                isWalkable,
                cell.TraversalCost,
                nextVersion);
            _version = nextVersion;
        }

        internal void SetCellTraversalCost(GridCell cell, int traversalCost)
        {
            if (cell == null || cell.TraversalCost == traversalCost)
            {
                return;
            }

            var nextVersion = GetNextVersion();
            _nonDefaultTraversalCostCellCount +=
                traversalCost == GridCell.DefaultTraversalCost ? -1 : 0;
            _nonDefaultTraversalCostCellCount +=
                cell.TraversalCost == GridCell.DefaultTraversalCost ? 1 : 0;
            cell.SetNavigationData(
                cell.IsWalkable,
                traversalCost,
                nextVersion);
            _version = nextVersion;
            LastTraversalCostChangeVersion = nextVersion;
        }

        private long GetNextVersion()
        {
            if (_version == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The navigation grid version has been exhausted.");
            }

            return _version + 1;
        }
    }

    /// <summary>
    /// Describes the complete navigation data to publish for one existing cell.
    /// </summary>
    public readonly struct GridCellNavigationUpdate
    {
        public GridCellNavigationUpdate(
            Vector2Int coordinates,
            bool isWalkable,
            int traversalCost)
        {
            if (traversalCost < GridCell.DefaultTraversalCost)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(traversalCost),
                    "Traversal cost must be at least one.");
            }

            Coordinates = coordinates;
            IsWalkable = isWalkable;
            TraversalCost = traversalCost;
        }

        public Vector2Int Coordinates { get; }

        public bool IsWalkable { get; }

        public int TraversalCost { get; }
    }

    /// <summary>
    /// Summarizes the effective changes made by an atomic grid update.
    /// </summary>
    public readonly struct GridNavigationChangeSummary
    {
        public GridNavigationChangeSummary(
            int changedCellCount,
            int changedWalkabilityCellCount,
            int changedTraversalCostCellCount)
        {
            ChangedCellCount = changedCellCount;
            ChangedWalkabilityCellCount = changedWalkabilityCellCount;
            ChangedTraversalCostCellCount = changedTraversalCostCellCount;
        }

        public int ChangedCellCount { get; }

        public int ChangedWalkabilityCellCount { get; }

        public int ChangedTraversalCostCellCount { get; }
    }
}
