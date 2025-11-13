using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Grid Cell
    /// </summary>
    public class GridCell
    {
        private readonly Vector2Int _coordenates;
        private readonly Vector3 _worldPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="GridCell"/> class.
        /// </summary>
        /// <param name="coordenates">The coordenates.</param>
        /// <param name="worldPosition">The world position.</param>
        public GridCell(Vector2Int coordenates, Vector3 worldPosition)
        {
            _coordenates = coordenates;
            _worldPosition = worldPosition;
            IsWalkable = true;
        }

        /// <summary>
        /// Gets the coordenates.
        /// </summary>
        /// <value>
        /// The coordenates.
        /// </value>
        public Vector2Int Coordenates => _coordenates;

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
        public bool IsWalkable { get; set; }

        /// <summary>
        /// Creates the path node.
        /// </summary>
        /// <returns></returns>
        public PathNode CreatePathNode() => new PathNode(this);
    }
}
