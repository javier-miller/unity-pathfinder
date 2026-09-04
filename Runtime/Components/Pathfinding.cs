using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Controls how much navigation-grid detail is drawn for a selected pathfinder.
    /// </summary>
    public enum PathfindingGizmoDetail
    {
        BoundsOnly = 0,
        SampledCells = 1
    }

    /// <summary>
    /// Filters the bounded cell sample used by navigation gizmos.
    /// </summary>
    public enum PathfindingGizmoCellFilter
    {
        All = 0,
        Blocked = 1,
        Walkable = 2,
        WeightedTerrain = 3,
        LatestChanges = 4
    }

    /// <summary>
    /// Unity-facing pathfinding facade. A dedicated grid source builds and maps scene
    /// data, while <see cref="GridPathfinder"/> performs the search.
    /// </summary>
    public abstract class Pathfinding :
        MonoBehaviour,
        IPathfinding,
        IVersionedPathfinding
    {
        private readonly GridPathfinder _gridPathfinder = new GridPathfinder();
        private IPathfindingGridSource _gridSource;
        private long _lastIssuedGridVersion;

        protected Grid _grid;

        /// <summary>
        /// Physics layers baked into every navigation snapshot.
        /// </summary>
        [SerializeField]
        private LayerMask staticObstacleMask;

        /// <summary>
        /// Moving non-agent obstacles. They are deliberately excluded from grid builds.
        /// </summary>
        [SerializeField]
        private LayerMask dynamicObstacleMask;

        /// <summary>
        /// Unit and character layers. They are deliberately excluded from grid builds.
        /// </summary>
        [SerializeField]
        private LayerMask agentMask;

        [SerializeField]
        protected bool showGizmos = true;

        [SerializeField]
        [Tooltip("Bounds are inexpensive. Sampled Cells inspects only the configured maximum while this object is selected.")]
        private PathfindingGizmoDetail gizmoDetail =
            PathfindingGizmoDetail.BoundsOnly;

        [SerializeField]
        [Tooltip("Filter applied after selecting a uniformly distributed, bounded cell sample.")]
        private PathfindingGizmoCellFilter gizmoCellFilter =
            PathfindingGizmoCellFilter.Blocked;

        [SerializeField]
        [Range(16, 4096)]
        [Tooltip("Maximum cells inspected per gizmo repaint. Large grids are sampled instead of traversed completely.")]
        private int maximumGizmoCellSamples = 512;

        [SerializeField]
        [Tooltip("Adds a translucent cube behind each sampled cell wireframe.")]
        private bool fillGizmoCells;

        /// <summary>
        /// Gets whether the most recent refresh produced a usable grid.
        /// </summary>
        public bool HasGrid => _grid != null;

        /// <summary>
        /// Gets the diagnostic from the most recent failed grid build.
        /// </summary>
        public string LastGridBuildError { get; private set; }

        /// <summary>
        /// Gets the diagnostic from the most recent failed regional update.
        /// </summary>
        public string LastGridUpdateError { get; private set; }

        /// <summary>
        /// Gets the version of the current navigation snapshot, or zero without a grid.
        /// </summary>
        public long GridVersion => _grid?.Version ?? 0;

        /// <summary>
        /// Gets whether the current snapshot contains non-default traversal costs.
        /// </summary>
        public bool HasWeightedTerrain => _grid?.HasWeightedTerrain ?? false;

        /// <summary>
        /// Gets whether navigation gizmos are enabled for this component.
        /// They are still drawn only while the GameObject is selected.
        /// </summary>
        public bool ShowGizmos => showGizmos;

        /// <summary>
        /// Gets the configured gizmo detail level.
        /// </summary>
        public PathfindingGizmoDetail GizmoDetail => gizmoDetail;

        /// <summary>
        /// Gets the filter applied to sampled cell gizmos.
        /// </summary>
        public PathfindingGizmoCellFilter GizmoCellFilter => gizmoCellFilter;

        /// <summary>
        /// Gets the maximum number of cells inspected by one gizmo repaint.
        /// </summary>
        public int MaximumGizmoCellSamples =>
            Mathf.Clamp(maximumGizmoCellSamples, 16, 4096);

        /// <summary>
        /// Gets the number of cells the current gizmo configuration can inspect.
        /// Zero means that only the constant-cost bounds are drawn.
        /// </summary>
        public int GizmoInspectedCellCount =>
            !showGizmos || _grid == null ||
            gizmoDetail == PathfindingGizmoDetail.BoundsOnly
                ? 0
                : Mathf.Min(_grid.CellCount, MaximumGizmoCellSamples);

        /// <summary>
        /// Gets the total number of cells in the active snapshot.
        /// </summary>
        public int GridCellCount => _grid?.CellCount ?? 0;

        /// <summary>
        /// Gets the summary of the most recent successful regional update.
        /// </summary>
        public GridRegionUpdateResult LastGridRegionUpdate { get; private set; }

        /// <summary>
        /// Gets the source created for the most recent refresh attempt.
        /// </summary>
        public IPathfindingGridSource GridSource => _gridSource;

        public LayerMask StaticObstacleMask => staticObstacleMask;

        public LayerMask DynamicObstacleMask => dynamicObstacleMask;

        public LayerMask AgentMask => agentMask;

        /// <summary>
        /// Gets layers that may obstruct movement at runtime but are never baked.
        /// </summary>
        public LayerMask RuntimeObstacleMask => new LayerMask
        {
            value = dynamicObstacleMask.value | agentMask.value
        };

        /// <summary>
        /// Configures the mutually exclusive physics roles used by this pathfinder.
        /// Useful for runtime-built scenes and procedural levels.
        /// </summary>
        public bool ConfigureObstacleMasks(
            LayerMask staticObstacles,
            LayerMask dynamicObstacles,
            LayerMask agents,
            bool refresh = true)
        {
            var previousStatic = staticObstacleMask;
            var previousDynamic = dynamicObstacleMask;
            var previousAgents = agentMask;
            staticObstacleMask = staticObstacles;
            dynamicObstacleMask = dynamicObstacles;
            agentMask = agents;
            if (!TryValidateObstacleLayers(out _))
            {
                staticObstacleMask = previousStatic;
                dynamicObstacleMask = previousDynamic;
                agentMask = previousAgents;
                return false;
            }

            if (refresh)
            {
                Refresh();
            }

            return !refresh || HasGrid;
        }

        protected virtual void Awake() => Refresh();

        /// <summary>
        /// Tries to resolve a walkable grid-cell center from a world position.
        /// </summary>
        public virtual bool TryGetWalkablePosition(
            Vector3 position,
            out Vector3 result)
        {
            result = default;
            if (_grid == null)
            {
                return false;
            }

            if (_gridSource == null ||
                !_gridSource.TryGetCellCoordinates(position, out var coordinates))
            {
                return false;
            }

            var cell = _grid.GetCell(coordinates.x, coordinates.y);
            if (cell == null || !cell.IsWalkable)
            {
                return false;
            }

            result = cell.WorldPosition;
            return true;
        }

        /// <summary>
        /// Finds a path and returns a detailed, immutable result.
        /// </summary>
        public virtual PathResult FindPath(
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            PathQueryOptions options)
        {
            if (_grid == null)
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    endWorldPosition)
                    .WithGridVersion(GridVersion);
            }

            if (_gridSource == null ||
                !_gridSource.TryGetCellCoordinates(startWorldPosition, out var start) ||
                !_gridSource.TryGetCellCoordinates(endWorldPosition, out var destination))
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    endWorldPosition)
                    .WithGridVersion(GridVersion);
            }

            return _gridPathfinder.FindPath(
                _grid,
                start,
                destination,
                endWorldPosition,
                options);
        }

        /// <summary>
        /// Resamples the cells intersected by world bounds without rebuilding grid geometry.
        /// </summary>
        public virtual bool TryRefreshRegion(
            Bounds worldBounds,
            out GridRegionUpdateResult result)
        {
            result = default;
            LastGridUpdateError = null;
            if (_grid == null || _gridSource == null)
            {
                LastGridUpdateError = "No grid is available for a regional update.";
                return false;
            }

            if (!TryValidateObstacleLayers(out var obstacleLayerError))
            {
                LastGridUpdateError = obstacleLayerError;
                return false;
            }

            if (_gridSource is not IIncrementalPathfindingGridSource incrementalSource)
            {
                LastGridUpdateError =
                    "The active grid source does not support regional updates.";
                return false;
            }

            if (!incrementalSource.TryRefreshRegion(
                    _grid,
                    worldBounds,
                    out result,
                    out var errorMessage))
            {
                LastGridUpdateError = string.IsNullOrWhiteSpace(errorMessage)
                    ? "The grid source could not refresh the requested region."
                    : errorMessage;
                return false;
            }

            _lastIssuedGridVersion = System.Math.Max(
                _lastIssuedGridVersion,
                result.CurrentVersion);
            LastGridRegionUpdate = result;
            return true;
        }

        /// <summary>
        /// Checks every grid cell crossed by the retained path against the current snapshot.
        /// </summary>
        public virtual bool IsPathWalkable(
            Vector3 startWorldPosition,
            IReadOnlyList<Vector3> waypoints,
            int firstWaypointIndex,
            long pathGridVersion,
            PathQueryOptions options = null)
        {
            if (_grid == null || _gridSource == null || waypoints == null ||
                firstWaypointIndex < 0 || firstWaypointIndex > waypoints.Count ||
                pathGridVersion < _grid.SnapshotVersion ||
                pathGridVersion < _grid.LastTraversalCostChangeVersion ||
                !_gridSource.TryGetCellCoordinates(
                    startWorldPosition,
                    out var startCoordinates))
            {
                return false;
            }

            var startCell = _grid.GetCell(startCoordinates.x, startCoordinates.y);
            if (startCell == null || !startCell.IsWalkable)
            {
                return false;
            }

            var queryOptions = options ?? PathQueryOptions.Default;
            var previousCoordinates = startCoordinates;
            for (var i = firstWaypointIndex; i < waypoints.Count; i++)
            {
                if (!_gridSource.TryGetCellCoordinates(
                        waypoints[i],
                        out var waypointCoordinates) ||
                    !GridPathfinder.HasLineOfSight(
                        _grid,
                        previousCoordinates,
                        waypointCoordinates,
                        queryOptions.AllowDiagonalMovement,
                        queryOptions.PreventCornerCutting,
                        queryOptions.AgentProfile.GetSanitizedRadius()))
                {
                    return false;
                }

                previousCoordinates = waypointCoordinates;
            }

            return true;
        }

        /// <summary>
        /// Rebuilds the navigation grid from its scene source.
        /// </summary>
        public virtual void Refresh()
        {
            _lastIssuedGridVersion = System.Math.Max(
                _lastIssuedGridVersion,
                GridVersion);
            _gridSource = null;
            _grid = null;
            LastGridBuildError = null;
            LastGridUpdateError = null;
            LastGridRegionUpdate = default;

            var source = CreateGridSource();
            _gridSource = source;
            if (!TryValidateConfiguration(source, out var configurationError))
            {
                SetGridBuildFailure(configurationError);
                return;
            }

            if (!source.TryBuildGrid(out var grid, out var errorMessage) || grid == null)
            {
                SetGridBuildFailure(
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "The grid source did not produce a grid."
                        : errorMessage);
                return;
            }

            _lastIssuedGridVersion = NextGridVersion(_lastIssuedGridVersion);
            grid.InitializeVersion(_lastIssuedGridVersion);
            _grid = grid;
        }

        /// <summary>
        /// Validates the current component and grid-source configuration without
        /// building or sampling the grid. Runtime refresh and Editor inspectors use
        /// this same preflight path.
        /// </summary>
        public bool TryValidateConfiguration(out string errorMessage) =>
            TryValidateConfiguration(CreateGridSource(), out errorMessage);

        /// <summary>
        /// Ensures each classified role owns a disjoint set of Physics2D layers.
        /// This prevents an agent or dynamic obstacle from being baked accidentally.
        /// </summary>
        public bool TryValidateObstacleLayers(out string errorMessage)
        {
            if (TryGetLayerOverlap(
                    staticObstacleMask,
                    dynamicObstacleMask,
                    "Static Obstacle Mask",
                    "Dynamic Obstacle Mask",
                    out errorMessage) ||
                TryGetLayerOverlap(
                    staticObstacleMask,
                    agentMask,
                    "Static Obstacle Mask",
                    "Agent Mask",
                    out errorMessage) ||
                TryGetLayerOverlap(
                    dynamicObstacleMask,
                    agentMask,
                    "Dynamic Obstacle Mask",
                    "Agent Mask",
                    out errorMessage))
            {
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool TryValidateConfiguration(
            IPathfindingGridSource source,
            out string errorMessage)
        {
            if (!TryValidateObstacleLayers(out errorMessage))
            {
                return false;
            }

            if (source == null)
            {
                errorMessage = "No grid source is configured.";
                return false;
            }

            if (source is IPathfindingGridSourceConfigurationValidator validator &&
                !validator.TryValidateConfiguration(out errorMessage))
            {
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Creates the source used by the next refresh.
        /// </summary>
        protected abstract IPathfindingGridSource CreateGridSource();

        private static bool TryGetLayerOverlap(
            LayerMask firstMask,
            LayerMask secondMask,
            string firstRole,
            string secondRole,
            out string errorMessage)
        {
            var overlappingLayers = firstMask.value & secondMask.value;
            if (overlappingLayers == 0)
            {
                errorMessage = null;
                return false;
            }

            errorMessage =
                $"{firstRole} and {secondRole} both include " +
                $"{FormatLayerNames(overlappingLayers)}. Each Physics2D layer must " +
                "have exactly one navigation role; remove the shared layer from one mask.";
            return true;
        }

        private static string FormatLayerNames(int layerMask)
        {
            var result = string.Empty;
            for (var layer = 0; layer < 32; layer++)
            {
                if ((layerMask & (1 << layer)) == 0)
                {
                    continue;
                }

                var layerName = LayerMask.LayerToName(layer);
                var description = string.IsNullOrEmpty(layerName)
                    ? $"layer {layer}"
                    : $"'{layerName}' (layer {layer})";
                result += string.IsNullOrEmpty(result)
                    ? description
                    : ", " + description;
            }

            return result;
        }

        private void SetGridBuildFailure(string errorMessage)
        {
            LastGridBuildError = errorMessage;
            Debug.LogError($"Unable to build pathfinding grid: {errorMessage}", this);
        }

        private static long NextGridVersion(long version)
        {
            if (version == long.MaxValue)
            {
                throw new System.InvalidOperationException(
                    "The navigation grid version has been exhausted.");
            }

            return version + 1;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || _grid == null)
            {
                return;
            }

            var previousColor = Gizmos.color;
            DrawGridBounds();
            if (gizmoDetail == PathfindingGizmoDetail.SampledCells)
            {
                DrawSampledGridCells();
            }

            Gizmos.color = previousColor;
        }

        private void DrawGridBounds()
        {
            var counts = _grid.CellsCount;
            var first = _grid.GetCell(0, 0);
            var lastX = _grid.GetCell(counts.x - 1, 0);
            var lastY = _grid.GetCell(0, counts.y - 1);
            var last = _grid.GetCell(counts.x - 1, counts.y - 1);
            if (first == null || lastX == null || lastY == null || last == null)
            {
                return;
            }

            var horizontalHalfCell = counts.x > 1
                ? (lastX.WorldPosition - first.WorldPosition) /
                  (counts.x - 1) * 0.5f
                : Vector3.right * _grid.CellSize.x * 0.5f;
            var verticalHalfCell = counts.y > 1
                ? (lastY.WorldPosition - first.WorldPosition) /
                  (counts.y - 1) * 0.5f
                : Vector3.up * _grid.CellSize.y * 0.5f;
            var bottomLeft = first.WorldPosition -
                             horizontalHalfCell - verticalHalfCell;
            var bottomRight = lastX.WorldPosition +
                              horizontalHalfCell - verticalHalfCell;
            var topRight = last.WorldPosition +
                           horizontalHalfCell + verticalHalfCell;
            var topLeft = lastY.WorldPosition -
                          horizontalHalfCell + verticalHalfCell;

            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.95f);
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }

        private void DrawSampledGridCells()
        {
            var totalCellCount = _grid.CellCount;
            var sampleCount = Mathf.Min(
                totalCellCount,
                MaximumGizmoCellSamples);
            if (sampleCount <= 0)
            {
                return;
            }

            var cellSize = new Vector3(
                _grid.CellSize.x * 0.9f,
                _grid.CellSize.y * 0.9f,
                0.01f);
            for (var sampleIndex = 0;
                 sampleIndex < sampleCount;
                 sampleIndex++)
            {
                var cellIndex = sampleCount == 1
                    ? 0
                    : (int)((long)sampleIndex * (totalCellCount - 1) /
                            (sampleCount - 1));
                var cell = _grid.GetCellByIndex(cellIndex);
                if (cell == null || !ShouldDrawGizmoCell(cell))
                {
                    continue;
                }

                var color = GetGizmoCellColor(cell);
                if (fillGizmoCells)
                {
                    Gizmos.color = new Color(
                        color.r,
                        color.g,
                        color.b,
                        0.18f);
                    Gizmos.DrawCube(cell.WorldPosition, cellSize);
                }

                Gizmos.color = color;
                Gizmos.DrawWireCube(cell.WorldPosition, cellSize);
            }
        }

        private bool ShouldDrawGizmoCell(GridCell cell)
        {
            switch (gizmoCellFilter)
            {
                case PathfindingGizmoCellFilter.Blocked:
                    return !cell.IsWalkable;
                case PathfindingGizmoCellFilter.Walkable:
                    return cell.IsWalkable;
                case PathfindingGizmoCellFilter.WeightedTerrain:
                    return cell.TraversalCost !=
                           GridCell.DefaultTraversalCost;
                case PathfindingGizmoCellFilter.LatestChanges:
                    return cell.LastNavigationDataChangedVersion ==
                           _grid.Version;
                default:
                    return true;
            }
        }

        private Color GetGizmoCellColor(GridCell cell)
        {
            if (!cell.IsWalkable)
            {
                return new Color(1f, 0.2f, 0.2f, 0.95f);
            }

            if (cell.TraversalCost != GridCell.DefaultTraversalCost)
            {
                return new Color(1f, 0.75f, 0.1f, 0.95f);
            }

            if (gizmoCellFilter == PathfindingGizmoCellFilter.LatestChanges)
            {
                return new Color(0.25f, 0.9f, 1f, 0.95f);
            }

            return new Color(0.2f, 1f, 0.35f, 0.95f);
        }
#endif
    }
}
