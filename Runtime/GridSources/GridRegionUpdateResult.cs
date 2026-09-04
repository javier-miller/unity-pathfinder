using System;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Immutable summary of one successful regional grid refresh.
    /// </summary>
    public readonly struct GridRegionUpdateResult
    {
        public GridRegionUpdateResult(
            Bounds requestedWorldBounds,
            RectInt evaluatedCellBounds,
            int evaluatedCellCount,
            int changedCellCount,
            int changedWalkabilityCellCount,
            int changedTraversalCostCellCount,
            long previousVersion,
            long currentVersion)
        {
            if (evaluatedCellCount < 0 ||
                changedCellCount < 0 ||
                changedWalkabilityCellCount < 0 ||
                changedTraversalCostCellCount < 0 ||
                changedCellCount > evaluatedCellCount ||
                changedWalkabilityCellCount > changedCellCount ||
                changedTraversalCostCellCount > changedCellCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(changedCellCount),
                    "Regional update cell counts are inconsistent.");
            }

            if (previousVersion < 0 || currentVersion < previousVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentVersion),
                    "Regional update versions are inconsistent.");
            }

            RequestedWorldBounds = requestedWorldBounds;
            EvaluatedCellBounds = evaluatedCellBounds;
            EvaluatedCellCount = evaluatedCellCount;
            ChangedCellCount = changedCellCount;
            ChangedWalkabilityCellCount = changedWalkabilityCellCount;
            ChangedTraversalCostCellCount = changedTraversalCostCellCount;
            PreviousVersion = previousVersion;
            CurrentVersion = currentVersion;
        }

        public Bounds RequestedWorldBounds { get; }

        /// <summary>
        /// Gets zero-based grid coordinates. RectInt maximum values are exclusive.
        /// </summary>
        public RectInt EvaluatedCellBounds { get; }

        public int EvaluatedCellCount { get; }

        public int ChangedCellCount { get; }

        public int ChangedWalkabilityCellCount { get; }

        public int ChangedTraversalCostCellCount { get; }

        public long PreviousVersion { get; }

        public long CurrentVersion { get; }

        public bool HasChanges => ChangedCellCount > 0;
    }
}
