using System;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Grid Cell
    /// </summary>
    public class GridCell
    {
        public const int DefaultTraversalCost = 1;

        private readonly Vector2Int _coordinates;
        private readonly Vector3 _worldPosition;
        private readonly Grid _owner;
        private bool _isWalkable;
        private int _traversalCost;

        /// <summary>
        /// Initializes a new instance of the <see cref="GridCell"/> class.
        /// </summary>
        /// <param name="coordinates">The coordinates.</param>
        /// <param name="worldPosition">The world position.</param>
        public GridCell(Vector2Int coordinates, Vector3 worldPosition)
        {
            _coordinates = coordinates;
            _worldPosition = worldPosition;
            _isWalkable = true;
            _traversalCost = DefaultTraversalCost;
            LastNavigationDataChangedVersion = 1;
        }

        internal GridCell(
            Vector2Int coordinates,
            Vector3 worldPosition,
            Grid owner,
            bool isWalkable,
            int traversalCost,
            long version)
        {
            _coordinates = coordinates;
            _worldPosition = worldPosition;
            _owner = owner;
            _isWalkable = isWalkable;
            _traversalCost = ValidateTraversalCost(traversalCost);
            LastNavigationDataChangedVersion = version;
        }

        /// <summary>
        /// Gets the coordinates.
        /// </summary>
        /// <value>
        /// The coordinates.
        /// </value>
        public Vector2Int Coordinates => _coordinates;

        /// <summary>
        /// Gets the world position.
        /// </summary>
        /// <value>
        /// The world position.
        /// </value>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is walkable.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is walkable; otherwise, <c>false</c>.
        /// </value>
        public bool IsWalkable
        {
            get => _isWalkable;
            set
            {
                if (_owner == null)
                {
                    _isWalkable = value;
                    return;
                }

                _owner.SetCellWalkability(this, value);
            }
        }

        /// <summary>
        /// Gets or sets the positive multiplier applied when entering this cell.
        /// A value of one is normal terrain; larger values make A* prefer alternatives.
        /// </summary>
        public int TraversalCost
        {
            get => _traversalCost;
            set
            {
                var validatedCost = ValidateTraversalCost(value);
                if (_owner == null)
                {
                    _traversalCost = validatedCost;
                    return;
                }

                _owner.SetCellTraversalCost(this, validatedCost);
            }
        }

        /// <summary>
        /// Gets the grid revision in which this cell last changed walkability or cost.
        /// </summary>
        public long LastNavigationDataChangedVersion { get; private set; }

        internal void SetNavigationData(
            bool isWalkable,
            int traversalCost,
            long version)
        {
            _isWalkable = isWalkable;
            _traversalCost = traversalCost;
            LastNavigationDataChangedVersion = version;
        }

        internal void SetNavigationDataChangedVersion(long version)
        {
            LastNavigationDataChangedVersion = version;
        }

        private static int ValidateTraversalCost(int traversalCost)
        {
            if (traversalCost < DefaultTraversalCost)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(traversalCost),
                    "Traversal cost must be at least one.");
            }

            return traversalCost;
        }
    }
}
