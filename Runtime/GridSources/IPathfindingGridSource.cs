using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Shared safety defaults for navigation-grid construction.
    /// </summary>
    public static class PathfindingGridLimits
    {
        /// <summary>
        /// Default upper bound used by built-in grid sources before allocating cells.
        /// </summary>
        public const int DefaultMaximumCellCount = 262144;
    }

    /// <summary>
    /// Builds navigation data from one source and maps world positions back to
    /// coordinates in the most recently built grid.
    /// </summary>
    public interface IPathfindingGridSource
    {
        /// <summary>
        /// Tries to build a complete grid snapshot.
        /// </summary>
        /// <param name="grid">The new grid when construction succeeds.</param>
        /// <param name="errorMessage">A diagnostic message when construction fails.</param>
        bool TryBuildGrid(out Grid grid, out string errorMessage);

        /// <summary>
        /// Tries to map a world position to coordinates in the latest grid snapshot.
        /// Coordinates outside the grid are valid results and are rejected later by
        /// the path query with the appropriate outside-grid status.
        /// </summary>
        bool TryGetCellCoordinates(
            Vector3 worldPosition,
            out Vector2Int cellCoordinates);
    }

    /// <summary>
    /// Optional preflight validation for grid sources. Implementations should only
    /// inspect configuration and must not build a grid or perform per-cell sampling.
    /// </summary>
    public interface IPathfindingGridSourceConfigurationValidator
    {
        /// <summary>
        /// Validates whether the source can attempt a grid build.
        /// </summary>
        /// <param name="errorMessage">
        /// The same actionable diagnostic used by runtime grid construction.
        /// </param>
        bool TryValidateConfiguration(out string errorMessage);
    }
}
