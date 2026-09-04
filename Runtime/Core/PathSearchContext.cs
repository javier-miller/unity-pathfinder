using System;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Owns the mutable storage reused by consecutive searches.
    /// A context can only serve one search at a time.
    /// </summary>
    internal sealed class PathSearchContext
    {
        private int[] _generations;
        private int _generation;

        public PathSearchContext()
        {
            WalkingCosts = Array.Empty<int>();
            Parents = Array.Empty<int>();
            States = Array.Empty<byte>();
            PathIndices = Array.Empty<int>();
            _generations = Array.Empty<int>();
            OpenQueue = new PathPriorityQueue(0);
        }

        public int[] WalkingCosts { get; private set; }

        public int[] Parents { get; private set; }

        public byte[] States { get; private set; }

        /// <summary>
        /// Reusable reconstruction buffer. It is also compacted in place by smoothing.
        /// </summary>
        public int[] PathIndices { get; private set; }

        public PathPriorityQueue OpenQueue { get; }

        /// <summary>
        /// Starts a new query without clearing every cell in the grid.
        /// Cells are initialized lazily for the current generation.
        /// </summary>
        public void Begin(int cellCount)
        {
            if (cellCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount));
            }

            EnsureCapacity(cellCount);
            OpenQueue.Reset(cellCount);

            if (_generation == int.MaxValue)
            {
                Array.Clear(_generations, 0, _generations.Length);
                _generation = 1;
                return;
            }

            _generation++;
        }

        /// <summary>
        /// Initializes a cell the first time it participates in the current query.
        /// </summary>
        public void InitializeCell(int cellIndex)
        {
            if (_generations[cellIndex] == _generation)
            {
                return;
            }

            _generations[cellIndex] = _generation;
            WalkingCosts[cellIndex] = int.MaxValue;
            Parents[cellIndex] = -1;
            States[cellIndex] = 0;
        }

        private void EnsureCapacity(int cellCount)
        {
            if (cellCount <= WalkingCosts.Length)
            {
                return;
            }

            Array.Resize(ref _generations, cellCount);

            var walkingCosts = WalkingCosts;
            var parents = Parents;
            var states = States;
            var pathIndices = PathIndices;
            Array.Resize(ref walkingCosts, cellCount);
            Array.Resize(ref parents, cellCount);
            Array.Resize(ref states, cellCount);
            Array.Resize(ref pathIndices, cellCount);
            WalkingCosts = walkingCosts;
            Parents = parents;
            States = states;
            PathIndices = pathIndices;
        }
    }
}
