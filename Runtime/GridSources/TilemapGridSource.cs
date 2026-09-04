using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Builds a bounded navigation snapshot from explicit Tilemap rules and optionally
    /// samples static blocked cells. Terrain, bounds and cost semantics are explicit.
    /// </summary>
    public sealed class TilemapGridSource :
        IPathfindingGridSource,
        IIncrementalPathfindingGridSource,
        IPathfindingGridSourceConfigurationValidator
    {
        private const float ColliderQueryScale = 0.9f;

        private readonly Tilemap _tilemap;
        private readonly IGridCellObstacleSampler _obstacleSampler;
        private readonly TilemapCellSemantics _cellSemantics;
        private readonly TilemapBoundsMode _boundsMode;
        private readonly bool _trimEmptyBorder;
        private readonly BoundsInt _explicitBounds;
        private readonly int _maximumGridCells;
        private readonly int _defaultTraversalCost;
        private readonly Dictionary<TileBase, TileNavigationData> _tileRules =
            new Dictionary<TileBase, TileNavigationData>();
        private readonly List<GridCellNavigationUpdate> _navigationUpdates =
            new List<GridCellNavigationUpdate>();

        private Vector3Int _boundsMinimum;
        private bool[] _occupiedCells;
        private Grid _builtGrid;
        private bool _hasBuiltGrid;

        public TilemapGridSource(
            Tilemap tilemap,
            IGridCellObstacleSampler obstacleSampler,
            TilemapGridSourceOptions options)
        {
            _tilemap = tilemap;
            options ??= new TilemapGridSourceOptions();
            _obstacleSampler = options.SampleStaticObstacles
                ? obstacleSampler
                : null;
            _cellSemantics = options.CellSemantics;
            _boundsMode = options.BoundsMode;
            _trimEmptyBorder = options.TrimEmptyBorder;
            _explicitBounds = options.ExplicitBounds;
            _maximumGridCells = Mathf.Max(1, options.MaximumGridCells);
            _defaultTraversalCost = Mathf.Max(
                GridCell.DefaultTraversalCost,
                options.DefaultTraversalCost);

            var rules = options.TileRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule?.Tile == null)
                {
                    continue;
                }

                _tileRules[rule.Tile] = new TileNavigationData(
                    rule.IsWalkable,
                    Mathf.Max(
                        GridCell.DefaultTraversalCost,
                        rule.TraversalCost));
            }
        }

        /// <summary>
        /// Gets the bounds used by the most recent successful build.
        /// </summary>
        public BoundsInt EffectiveBounds { get; private set; }

        public int CandidateCellCount { get; private set; }

        public int OccupiedTileCount { get; private set; }

        public int WalkableCellCount { get; private set; }

        public bool SamplesStaticObstacles => _obstacleSampler != null;

        public int TileRuleCount => _tileRules.Count;

        public bool TryBuildGrid(out Grid grid, out string errorMessage)
        {
            grid = null;
            errorMessage = null;
            _hasBuiltGrid = false;
            _builtGrid = null;
            _occupiedCells = null;
            EffectiveBounds = default;
            CandidateCellCount = 0;
            OccupiedTileCount = 0;
            WalkableCellCount = 0;

            if (!TryValidateConfiguration(out errorMessage))
            {
                return false;
            }

            var cellSize = (Vector2)_tilemap.cellSize;
            var bounds = _boundsMode == TilemapBoundsMode.ExplicitBounds
                ? _explicitBounds
                : _tilemap.cellBounds;
            if (_boundsMode == TilemapBoundsMode.TilemapCellBounds &&
                _trimEmptyBorder &&
                !TryGetTightOccupiedBounds(bounds, out bounds))
            {
                errorMessage =
                    "Tilemap cell bounds do not contain any tiles. Paint navigation " +
                    "terrain or choose explicit bounds with the entire-bounds semantics.";
                return false;
            }

            if (!TryValidateBounds(bounds, "Effective Tilemap bounds", out errorMessage))
            {
                return false;
            }

            var cellCounts = new Vector2Int(bounds.size.x, bounds.size.y);
            _boundsMinimum = bounds.min;
            var result = new Grid(cellCounts, cellSize);
            var occupiedCells = new bool[result.CellCount];
            var colliderCellSize = cellSize * ColliderQueryScale;
            for (var x = 0; x < cellCounts.x; x++)
            {
                for (var y = 0; y < cellCounts.y; y++)
                {
                    var coordinates = new Vector2Int(x, y);
                    var tileCoordinates = new Vector3Int(
                        x + _boundsMinimum.x,
                        y + _boundsMinimum.y,
                        _boundsMinimum.z);
                    var worldPosition = _tilemap.GetCellCenterWorld(tileCoordinates);
                    var tile = _tilemap.GetTile(tileCoordinates);
                    var hasTile = tile != null;
                    if (hasTile)
                    {
                        OccupiedTileCount++;
                    }

                    occupiedCells[result.GetIndex(x, y)] = hasTile;

                    GetNavigationData(
                        tile,
                        hasTile,
                        worldPosition,
                        colliderCellSize,
                        out var isWalkable,
                        out var traversalCost);
                    if (isWalkable)
                    {
                        WalkableCellCount++;
                    }

                    result.AddCell(
                        coordinates,
                        worldPosition,
                        isWalkable,
                        traversalCost);
                }
            }

            if (_cellSemantics == TilemapCellSemantics.TilesDefineNavigableArea &&
                OccupiedTileCount == 0)
            {
                errorMessage =
                    "The effective Tilemap bounds contain no terrain tiles. Paint at " +
                    "least one tile inside the selected bounds or use the " +
                    "entire-bounds semantics intentionally.";
                return false;
            }

            grid = result;
            _builtGrid = result;
            _occupiedCells = occupiedCells;
            EffectiveBounds = bounds;
            CandidateCellCount = result.CellCount;
            _hasBuiltGrid = true;
            return true;
        }

        /// <summary>
        /// Validates Tilemap references, semantics and source bounds without scanning
        /// tiles or sampling Physics2D. Content-dependent failures remain build errors.
        /// </summary>
        public bool TryValidateConfiguration(out string errorMessage)
        {
            if (_tilemap == null)
            {
                errorMessage = "A Tilemap component is required to build navigation.";
                return false;
            }

            var cellSize = (Vector2)_tilemap.cellSize;
            if (!IsFinitePositive(cellSize.x) || !IsFinitePositive(cellSize.y))
            {
                errorMessage =
                    "Tilemap cell size must contain finite positive X/Y values.";
                return false;
            }

            if (!IsSupportedConfiguration(out errorMessage))
            {
                return false;
            }

            var bounds = _boundsMode == TilemapBoundsMode.ExplicitBounds
                ? _explicitBounds
                : _tilemap.cellBounds;
            var boundsName = _boundsMode == TilemapBoundsMode.ExplicitBounds
                ? "Explicit Tilemap bounds"
                : "Tilemap cell bounds";
            return TryValidateBounds(bounds, boundsName, out errorMessage);
        }

        public bool TryRefreshRegion(
            Grid grid,
            Bounds worldBounds,
            out GridRegionUpdateResult result,
            out string errorMessage)
        {
            result = default;
            errorMessage = null;
            if (!_hasBuiltGrid || _tilemap == null ||
                grid == null || grid != _builtGrid ||
                _occupiedCells == null)
            {
                errorMessage = "The Tilemap source can only update its latest built grid.";
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
            var colliderCellSize = (Vector2)_tilemap.cellSize * ColliderQueryScale;
            for (var x = cellBounds.xMin; x < cellBounds.xMax; x++)
            {
                for (var y = cellBounds.yMin; y < cellBounds.yMax; y++)
                {
                    var coordinates = new Vector2Int(x, y);
                    var tileCoordinates = new Vector3Int(
                        x + _boundsMinimum.x,
                        y + _boundsMinimum.y,
                        _boundsMinimum.z);
                    var worldPosition = _tilemap.GetCellCenterWorld(tileCoordinates);
                    var tile = _tilemap.GetTile(tileCoordinates);
                    var hasTile = tile != null;
                    var index = grid.GetIndex(x, y);
                    if (_occupiedCells[index] != hasTile)
                    {
                        OccupiedTileCount += hasTile ? 1 : -1;
                        _occupiedCells[index] = hasTile;
                    }

                    GetNavigationData(
                        tile,
                        hasTile,
                        worldPosition,
                        colliderCellSize,
                        out var isWalkable,
                        out var traversalCost);
                    var cell = grid.GetCell(x, y);
                    if (cell != null &&
                        (cell.IsWalkable != isWalkable ||
                         cell.TraversalCost != traversalCost))
                    {
                        if (cell.IsWalkable != isWalkable)
                        {
                            WalkableCellCount += isWalkable ? 1 : -1;
                        }

                        _navigationUpdates.Add(
                            new GridCellNavigationUpdate(
                                coordinates,
                                isWalkable,
                                traversalCost));
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
            if (!_hasBuiltGrid || _tilemap == null)
            {
                return false;
            }

            var tileCoordinates = _tilemap.WorldToCell(worldPosition);
            cellCoordinates = new Vector2Int(
                tileCoordinates.x - _boundsMinimum.x,
                tileCoordinates.y - _boundsMinimum.y);
            return true;
        }

        private bool IsSupportedConfiguration(out string errorMessage)
        {
            errorMessage = null;
            if (_cellSemantics != TilemapCellSemantics.TilesDefineNavigableArea &&
                _cellSemantics != TilemapCellSemantics.EntireBoundsDefineNavigableArea)
            {
                errorMessage = "The Tilemap cell semantics value is not supported.";
                return false;
            }

            if (_boundsMode != TilemapBoundsMode.TilemapCellBounds &&
                _boundsMode != TilemapBoundsMode.ExplicitBounds)
            {
                errorMessage = "The Tilemap bounds mode value is not supported.";
                return false;
            }

            return true;
        }

        private bool TryValidateBounds(
            BoundsInt bounds,
            string boundsName,
            out string errorMessage)
        {
            errorMessage = null;
            if (bounds.size.x < 1 || bounds.size.y < 1 || bounds.size.z != 1)
            {
                errorMessage = $"{boundsName} must contain at least one cell on X/Y and exactly one Z layer.";
                return false;
            }

            var cellCount = (long)bounds.size.x * bounds.size.y;
            if (cellCount > _maximumGridCells)
            {
                errorMessage =
                    $"{boundsName} contains {cellCount} cells, exceeding the configured " +
                    $"maximum of {_maximumGridCells}. Use explicit bounds, remove distant tiles " +
                    "or compress the Tilemap bounds.";
                return false;
            }

            return true;
        }

        private bool TryGetTightOccupiedBounds(
            BoundsInt sourceBounds,
            out BoundsInt occupiedBounds)
        {
            var foundTile = false;
            var minimumX = int.MaxValue;
            var minimumY = int.MaxValue;
            var maximumX = int.MinValue;
            var maximumY = int.MinValue;
            var z = sourceBounds.zMin;

            for (var x = sourceBounds.xMin; x < sourceBounds.xMax; x++)
            {
                for (var y = sourceBounds.yMin; y < sourceBounds.yMax; y++)
                {
                    if (!_tilemap.HasTile(new Vector3Int(x, y, z)))
                    {
                        continue;
                    }

                    foundTile = true;
                    minimumX = Mathf.Min(minimumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumX = Mathf.Max(maximumX, x);
                    maximumY = Mathf.Max(maximumY, y);
                }
            }

            occupiedBounds = foundTile
                ? new BoundsInt(
                    minimumX,
                    minimumY,
                    z,
                    maximumX - minimumX + 1,
                    maximumY - minimumY + 1,
                    1)
                : default;
            return foundTile;
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value > 0f;

        private bool TryGetAffectedCellBounds(
            Grid grid,
            Bounds worldBounds,
            out RectInt cellBounds)
        {
            var minimum = worldBounds.min;
            var maximum = worldBounds.max;
            var cornerA = _tilemap.WorldToCell(
                new Vector3(minimum.x, minimum.y, worldBounds.center.z));
            var cornerB = _tilemap.WorldToCell(
                new Vector3(minimum.x, maximum.y, worldBounds.center.z));
            var cornerC = _tilemap.WorldToCell(
                new Vector3(maximum.x, minimum.y, worldBounds.center.z));
            var cornerD = _tilemap.WorldToCell(
                new Vector3(maximum.x, maximum.y, worldBounds.center.z));

            var minimumX = Mathf.Min(cornerA.x, cornerB.x, cornerC.x, cornerD.x) -
                _boundsMinimum.x - 1;
            var minimumY = Mathf.Min(cornerA.y, cornerB.y, cornerC.y, cornerD.y) -
                _boundsMinimum.y - 1;
            var maximumX = Mathf.Max(cornerA.x, cornerB.x, cornerC.x, cornerD.x) -
                _boundsMinimum.x + 1;
            var maximumY = Mathf.Max(cornerA.y, cornerB.y, cornerC.y, cornerD.y) -
                _boundsMinimum.y + 1;

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

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private void GetNavigationData(
            TileBase tile,
            bool hasTile,
            Vector3 worldPosition,
            Vector2 colliderCellSize,
            out bool isWalkable,
            out int traversalCost)
        {
            var containsTerrain =
                _cellSemantics == TilemapCellSemantics.EntireBoundsDefineNavigableArea ||
                hasTile;
            isWalkable = containsTerrain;
            traversalCost = _defaultTraversalCost;
            if (tile != null && _tileRules.TryGetValue(tile, out var tileData))
            {
                isWalkable &= tileData.IsWalkable;
                traversalCost = tileData.TraversalCost;
            }

            if (isWalkable &&
                _obstacleSampler != null &&
                _obstacleSampler.IsBlocked(worldPosition, colliderCellSize))
            {
                isWalkable = false;
            }
        }

        private readonly struct TileNavigationData
        {
            public TileNavigationData(bool isWalkable, int traversalCost)
            {
                IsWalkable = isWalkable;
                TraversalCost = traversalCost;
            }

            public bool IsWalkable { get; }

            public int TraversalCost { get; }
        }
    }
}
