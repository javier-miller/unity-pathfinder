namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Path Node
    /// </summary>
    public class PathNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PathNode"/> class.
        /// </summary>
        /// <param name="cell">The cell.</param>
        public PathNode(GridCell cell)
        {
            Cell = cell;
            Reset();
        }

        /// <summary>
        /// Gets the cell.
        /// </summary>
        /// <value>
        /// The cell.
        /// </value>
        public GridCell Cell { get; }

        /// <summary>
        /// Gets the walking cost.
        /// </summary>
        /// <value>
        /// The walking cost.
        /// </value>
        public int WalkingCost { get; private set; }

        /// <summary>
        /// Gets the total cost.
        /// </summary>
        /// <value>
        /// The total cost.
        /// </value>
        public int TotalCost { get; private set; }

        /// <summary>
        /// Gets the heuristic cost.
        /// </summary>
        /// <value>
        /// The heuristic cost.
        /// </value>
        public int HeuristicCost { get; private set; }

        /// <summary>
        /// Gets the source node.
        /// </summary>
        /// <value>
        /// The source node.
        /// </value>
        public PathNode SourceNode { get; private set; }

        /// <summary>
        /// Sets the source.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public PathNode SetSource(PathNode node)
        {
            SourceNode = node;
            return this;
        }

        /// <summary>
        /// Sets the heuristic cost.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public PathNode SetHeuristicCost(int value)
        {
            HeuristicCost = value;
            return this;
        }

        /// <summary>
        /// Sets the costs.
        /// </summary>
        /// <param name="walkingCost">The walking cost.</param>
        /// <param name="heuristicCost">The heuristic cost.</param>
        /// <returns></returns>
        public PathNode SetCosts(int walkingCost, int heuristicCost)
        {
            WalkingCost = walkingCost;
            HeuristicCost = heuristicCost;
            CalculateFCost();

            return this;
        }

        /// <summary>
        /// Sets the walking cost.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public PathNode SetWalkingCost(int value)
        {
            WalkingCost = value;
            return this;
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        private void Reset()
        {
            WalkingCost = 99999999;
            CalculateFCost();
            SourceNode = null;
        }

        /// <summary>
        /// Calculates the f cost.
        /// </summary>
        private void CalculateFCost() => TotalCost = WalkingCost + HeuristicCost;
    }
}
