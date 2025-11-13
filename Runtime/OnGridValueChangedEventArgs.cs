using System;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// OnGridValueChangedEventArgs
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class OnGridValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnGridValueChangedEventArgs"/> class.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public OnGridValueChangedEventArgs(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Gets the x.
        /// </summary>
        /// <value>
        /// The x.
        /// </value>
        public int x { get; }

        /// <summary>
        /// Gets the y.
        /// </summary>
        /// <value>
        /// The y.
        /// </value>
        public int y { get; }
    }
}
