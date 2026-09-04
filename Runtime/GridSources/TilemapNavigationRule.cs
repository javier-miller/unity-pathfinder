using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Maps one Tile asset to navigation data without exposing that asset to A*.
    /// </summary>
    [Serializable]
    public sealed class TilemapNavigationRule
    {
        [SerializeField]
        private TileBase tile;

        [SerializeField]
        private bool isWalkable = true;

        [SerializeField]
        [Min(GridCell.DefaultTraversalCost)]
        private int traversalCost = GridCell.DefaultTraversalCost;

        public TileBase Tile
        {
            get => tile;
            set => tile = value;
        }

        public bool IsWalkable
        {
            get => isWalkable;
            set => isWalkable = value;
        }

        public int TraversalCost
        {
            get => traversalCost;
            set => traversalCost = Mathf.Max(
                GridCell.DefaultTraversalCost,
                value);
        }

        internal void Sanitize()
        {
            traversalCost = Mathf.Max(
                GridCell.DefaultTraversalCost,
                traversalCost);
        }
    }
}
