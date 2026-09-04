using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Rectangle
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    public class PathfindingRectangle : Pathfinding
    {
        private const float MinimumSize = 0.01f;
        [SerializeField]
        private Vector2 gridSize = new Vector2(100, 100);

        [SerializeField]
        private Vector2 tileSize = new Vector2(10, 10);

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum number of cells the rectangle may allocate during a grid build.")]
        private int maximumGridCells =
            PathfindingGridLimits.DefaultMaximumCellCount;

        public Vector2 GridSize => gridSize;

        public Vector2 CellSize => tileSize;

        public int MaximumGridCells => maximumGridCells > 0
            ? maximumGridCells
            : PathfindingGridLimits.DefaultMaximumCellCount;

        /// <summary>
        /// Reconfigures a runtime rectangle and optionally rebuilds its grid.
        /// </summary>
        public void Configure(
            Vector2 worldGridSize,
            Vector2 worldCellSize,
            bool refresh = true)
        {
            gridSize = worldGridSize;
            tileSize = worldCellSize;
            SanitizeConfiguration();
            if (refresh)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Reconfigures a runtime rectangle with an explicit cell-allocation budget.
        /// </summary>
        public void Configure(
            Vector2 worldGridSize,
            Vector2 worldCellSize,
            int maximumCellCount,
            bool refresh = true)
        {
            gridSize = worldGridSize;
            tileSize = worldCellSize;
            maximumGridCells = maximumCellCount;
            SanitizeConfiguration();
            if (refresh)
            {
                Refresh();
            }
        }

        protected override void Awake()
        {
            SanitizeConfiguration();
            base.Awake();
        }

        protected override IPathfindingGridSource CreateGridSource() =>
            new RectangleGridSource(
                transform.position,
                gridSize,
                tileSize,
                StaticObstacleMask.value == 0
                    ? null
                    : new Physics2DStaticObstacleSampler(StaticObstacleMask),
                MaximumGridCells);

        /// <summary>
        /// Called when [validate].
        /// </summary>
        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        private void SanitizeConfiguration()
        {
            tileSize = new Vector2(
                SanitizeSize(tileSize.x, MinimumSize),
                SanitizeSize(tileSize.y, MinimumSize));

            gridSize = new Vector2(
                SanitizeSize(gridSize.x, tileSize.x),
                SanitizeSize(gridSize.y, tileSize.y));

            gridSize = Vector2.Max(gridSize, tileSize);
            maximumGridCells = maximumGridCells > 0
                ? maximumGridCells
                : PathfindingGridLimits.DefaultMaximumCellCount;
        }

        private static float SanitizeSize(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Max(MinimumSize, value);
        }
    }
}
