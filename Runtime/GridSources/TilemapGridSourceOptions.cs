using System;
using System.Collections.Generic;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Defines which positions inside the selected Tilemap bounds represent terrain.
    /// </summary>
    public enum TilemapCellSemantics
    {
        /// <summary>
        /// A tile marks potential navigation terrain. Empty positions are blocked holes.
        /// </summary>
        TilesDefineNavigableArea = 0,

        /// <summary>
        /// Every position in the effective bounds is potential terrain, including holes.
        /// Use this when the bounds, rather than painted tiles, define the terrain.
        /// </summary>
        EntireBoundsDefineNavigableArea = 1
    }

    /// <summary>
    /// Selects where the rectangular navigation limits come from.
    /// </summary>
    public enum TilemapBoundsMode
    {
        TilemapCellBounds = 0,
        ExplicitBounds = 1
    }

    /// <summary>
    /// Serializable Tilemap grid configuration captured by a <see cref="TilemapGridSource"/>.
    /// </summary>
    [Serializable]
    public sealed class TilemapGridSourceOptions
    {
        public const int DefaultMaximumGridCells =
            PathfindingGridLimits.DefaultMaximumCellCount;

        [SerializeField]
        private TilemapCellSemantics cellSemantics =
            TilemapCellSemantics.TilesDefineNavigableArea;

        [SerializeField]
        private TilemapBoundsMode boundsMode = TilemapBoundsMode.TilemapCellBounds;

        [SerializeField]
        private bool trimEmptyBorder = true;

        [SerializeField]
        private BoundsInt explicitBounds = new BoundsInt(
            Vector3Int.zero,
            new Vector3Int(64, 64, 1));

        [SerializeField]
        [Min(1)]
        private int maximumGridCells = DefaultMaximumGridCells;

        [SerializeField]
        [Min(GridCell.DefaultTraversalCost)]
        private int defaultTraversalCost = GridCell.DefaultTraversalCost;

        [SerializeField]
        private List<TilemapNavigationRule> tileRules =
            new List<TilemapNavigationRule>();

        [SerializeField]
        private bool sampleStaticObstacles = true;

        public TilemapCellSemantics CellSemantics
        {
            get => cellSemantics;
            set => cellSemantics = value;
        }

        public TilemapBoundsMode BoundsMode
        {
            get => boundsMode;
            set => boundsMode = value;
        }

        public bool TrimEmptyBorder
        {
            get => trimEmptyBorder;
            set => trimEmptyBorder = value;
        }

        public BoundsInt ExplicitBounds
        {
            get => explicitBounds;
            set => explicitBounds = value;
        }

        public int MaximumGridCells
        {
            get => maximumGridCells;
            set => maximumGridCells = Mathf.Max(1, value);
        }

        public int DefaultTraversalCost
        {
            get => defaultTraversalCost;
            set => defaultTraversalCost = Mathf.Max(
                GridCell.DefaultTraversalCost,
                value);
        }

        public IList<TilemapNavigationRule> TileRules =>
            GetTileRules();

        /// <summary>
        /// Gets or sets whether the Tilemap source also samples StaticObstacleMask.
        /// Disable this for navigation encoded completely by tiles.
        /// </summary>
        public bool SampleStaticObstacles
        {
            get => sampleStaticObstacles;
            set => sampleStaticObstacles = value;
        }

        internal void Sanitize()
        {
            maximumGridCells = Mathf.Max(1, maximumGridCells);
            defaultTraversalCost = Mathf.Max(
                GridCell.DefaultTraversalCost,
                defaultTraversalCost);
            var rules = GetTileRules();
            for (var i = 0; i < rules.Count; i++)
            {
                rules[i]?.Sanitize();
            }
            var size = explicitBounds.size;
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            size.z = 1;
            explicitBounds.size = size;
        }

        private List<TilemapNavigationRule> GetTileRules()
        {
            tileRules ??= new List<TilemapNavigationRule>();
            return tileRules;
        }
    }
}
