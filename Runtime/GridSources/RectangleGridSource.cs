using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Builds a centered rectangular grid and optionally samples its blocked cells.
    /// Configuration is captured when the source is created and position mapping uses
    /// the same geometry snapshot that produced the grid.
    /// </summary>
    public sealed class RectangleGridSource :
        IPathfindingGridSource,
        IIncrementalPathfindingGridSource,
        IPathfindingGridSourceConfigurationValidator
    {
        private const float MinimumSize = 0.01f;
        private const float ColliderQueryScale = 0.9f;

        private readonly Vector3 _center;
        private readonly Vector2 _gridSize;
        private readonly Vector2 _cellSize;
        private readonly int _maximumGridCells;
        private readonly IGridCellObstacleSampler _obstacleSampler;
        private readonly List<GridCellNavigationUpdate> _navigationUpdates =
            new List<GridCellNavigationUpdate>();

        private Vector3 _firstCellCenter;
        private Grid _builtGrid;
        private bool _hasBuiltGrid;

        public RectangleGridSource(
            Vector3 center,
            Vector2 gridSize,
            Vector2 cellSize,
            IGridCellObstacleSampler obstacleSampler)
            : this(
                center,
                gridSize,
                cellSize,
                obstacleSampler,
                PathfindingGridLimits.DefaultMaximumCellCount)
        {
        }

        public RectangleGridSource(
            Vector3 center,
            Vector2 gridSize,
            Vector2 cellSize,
            IGridCellObstacleSampler obstacleSampler,
            int maximumGridCells)
        {
            _center = center;
            _gridSize = gridSize;
            _cellSize = cellSize;
            _obstacleSampler = obstacleSampler;
            _maximumGridCells = Mathf.Max(1, maximumGridCells);
        }

        public bool TryBuildGrid(out Grid grid, out string errorMessage)
        {
            grid = null;
            errorMessage = null;
            _hasBuiltGrid = false;
            _builtGrid = null;

            if (!TryValidateConfiguration(out errorMessage))
            {
                return false;
            }

            CalculateCellCounts(out var cellsX, out var cellsY);
            var cellCounts = new Vector2Int(cellsX, cellsY);
            var offset = new Vector2(
                (cellCounts.x - 1) * _cellSize.x * 0.5f,
                (cellCounts.y - 1) * _cellSize.y * 0.5f);
            _firstCellCenter = _center - new Vector3(offset.x, offset.y, 0f);

            var result = new Grid(cellCounts, _cellSize);
            var colliderCellSize = _cellSize * ColliderQueryScale;
            for (var x = 0; x < cellCounts.x; x++)
            {
                for (var y = 0; y < cellCounts.y; y++)
                {
                    var coordinates = new Vector2Int(x, y);
                    var worldPosition = _firstCellCenter + new Vector3(
                        x * _cellSize.x,
                        y * _cellSize.y,
                        0f);
                    var isBlocked = _obstacleSampler != null &&
                        _obstacleSampler.IsBlocked(worldPosition, colliderCellSize);
                    result.AddCell(coordinates, worldPosition, !isBlocked);
                }
            }

            grid = result;
            _builtGrid = result;
            _hasBuiltGrid = true;
            return true;
        }

        /// <summary>
        /// Validates rectangle geometry without allocating or sampling grid cells.
        /// </summary>
        public bool TryValidateConfiguration(out string errorMessage)
        {
            if (!IsFinite(_center.x) ||
                !IsFinite(_center.y) ||
                !IsFinite(_center.z))
            {
                errorMessage = "Rectangle grid center must contain finite values.";
                return false;
            }

            if (!IsFinitePositive(_gridSize.x) ||
                !IsFinitePositive(_gridSize.y) ||
                !IsFinitePositive(_cellSize.x) ||
                !IsFinitePositive(_cellSize.y))
            {
                errorMessage = "Grid size and cell size must contain finite positive values.";
                return false;
            }

            var cellsX = System.Math.Floor((double)_gridSize.x / _cellSize.x);
            var cellsY = System.Math.Floor((double)_gridSize.y / _cellSize.y);
            if (cellsX < 1d || cellsY < 1d)
            {
                errorMessage =
                    "Rectangle Grid Size must contain at least one complete Cell Size " +
                    "on each axis. Increase Grid Size or reduce Cell Size.";
                return false;
            }

            var requestedCellCount = cellsX * cellsY;
            if (cellsX > int.MaxValue ||
                cellsY > int.MaxValue ||
                requestedCellCount > _maximumGridCells)
            {
                errorMessage =
                    $"Rectangle grid requires {requestedCellCount:0} cells " +
                    $"({cellsX:0} x {cellsY:0}), exceeding Maximum Grid Cells " +
                    $"({_maximumGridCells}). Increase Cell Size, reduce Grid Size, " +
                    "or raise the maximum deliberately.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private void CalculateCellCounts(out int cellsX, out int cellsY)
        {
            cellsX = (int)System.Math.Floor((double)_gridSize.x / _cellSize.x);
            cellsY = (int)System.Math.Floor((double)_gridSize.y / _cellSize.y);
        }

        public bool TryRefreshRegion(
            Grid grid,
            Bounds worldBounds,
            out GridRegionUpdateResult result,
            out string errorMessage)
        {
            result = default;
            errorMessage = null;
            if (!_hasBuiltGrid || grid == null || grid != _builtGrid)
            {
                errorMessage = "The rectangle source can only update its latest built grid.";
                return false;
            }

            if (!TryValidateWorldBounds(worldBounds, out errorMessage))
            {
                return false;
            }

            var previousVersion = grid.Version;
            if (!TryGetAffectedCellBounds(grid, worldBounds, out var cellBounds))
            {
                result = new GridRegionUpdateResult(
                    worldBounds,
                    default,
                    0,
                    0,
                    0,
                    0,
                    previousVersion,
                    previousVersion);
                return true;
            }

            _navigationUpdates.Clear();
            var colliderCellSize = _cellSize * ColliderQueryScale;
            for (var x = cellBounds.xMin; x < cellBounds.xMax; x++)
            {
                for (var y = cellBounds.yMin; y < cellBounds.yMax; y++)
                {
                    var coordinates = new Vector2Int(x, y);
                    var worldPosition = _firstCellCenter + new Vector3(
                        x * _cellSize.x,
                        y * _cellSize.y,
                        0f);
                    var isBlocked = _obstacleSampler != null &&
                        _obstacleSampler.IsBlocked(worldPosition, colliderCellSize);
                    var cell = grid.GetCell(x, y);
                    if (cell != null && cell.IsWalkable != !isBlocked)
                    {
                        _navigationUpdates.Add(
                            new GridCellNavigationUpdate(
                                coordinates,
                                !isBlocked,
                                cell.TraversalCost));
                    }
                }
            }

            var changes = grid.ApplyNavigationUpdates(_navigationUpdates);
            result = new GridRegionUpdateResult(
                worldBounds,
                cellBounds,
                cellBounds.width * cellBounds.height,
                changes.ChangedCellCount,
                changes.ChangedWalkabilityCellCount,
                changes.ChangedTraversalCostCellCount,
                previousVersion,
                grid.Version);
            return true;
        }

        public bool TryGetCellCoordinates(
            Vector3 worldPosition,
            out Vector2Int cellCoordinates)
        {
            cellCoordinates = default;
            if (!_hasBuiltGrid)
            {
                return false;
            }

            var relativePosition = worldPosition - _firstCellCenter;
            cellCoordinates = new Vector2Int(
                Mathf.FloorToInt((relativePosition.x + _cellSize.x * 0.5f) / _cellSize.x),
                Mathf.FloorToInt((relativePosition.y + _cellSize.y * 0.5f) / _cellSize.y));
            return true;
        }

        private static bool IsFinitePositive(float value) =>
            IsFinite(value) && value >= MinimumSize;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private bool TryGetAffectedCellBounds(
            Grid grid,
            Bounds worldBounds,
            out RectInt cellBounds)
        {
            var halfSampleSize = _cellSize * ColliderQueryScale * 0.5f;
            var minimumX = Mathf.CeilToInt(
                (worldBounds.min.x - halfSampleSize.x - _firstCellCenter.x) /
                _cellSize.x);
            var minimumY = Mathf.CeilToInt(
                (worldBounds.min.y - halfSampleSize.y - _firstCellCenter.y) /
                _cellSize.y);
            var maximumX = Mathf.FloorToInt(
                (worldBounds.max.x + halfSampleSize.x - _firstCellCenter.x) /
                _cellSize.x);
            var maximumY = Mathf.FloorToInt(
                (worldBounds.max.y + halfSampleSize.y - _firstCellCenter.y) /
                _cellSize.y);

            minimumX = Mathf.Max(0, minimumX);
            minimumY = Mathf.Max(0, minimumY);
            maximumX = Mathf.Min(grid.CellsCount.x - 1, maximumX);
            maximumY = Mathf.Min(grid.CellsCount.y - 1, maximumY);
            if (minimumX > maximumX || minimumY > maximumY)
            {
                cellBounds = default;
                return false;
            }

            cellBounds = new RectInt(
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
            return true;
        }

        private static bool TryValidateWorldBounds(
            Bounds worldBounds,
            out string errorMessage)
        {
            var center = worldBounds.center;
            var size = worldBounds.size;
            if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(center.z) ||
                !IsFinite(size.x) || !IsFinite(size.y) || !IsFinite(size.z) ||
                size.x < 0f || size.y < 0f || size.z < 0f)
            {
                errorMessage = "Regional update bounds must contain finite, non-negative values.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
