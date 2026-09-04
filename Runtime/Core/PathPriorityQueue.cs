using System;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Binary min-heap for the cells participating in one path query.
    /// Each cell can occupy at most one heap position.
    /// </summary>
    internal sealed class PathPriorityQueue
    {
        private int[] _heap;
        private int[] _positions;
        private int[] _totalCosts;
        private int[] _heuristicCosts;
        private int _count;

        public PathPriorityQueue(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _heap = new int[capacity];
            _positions = new int[capacity];
            _totalCosts = new int[capacity];
            _heuristicCosts = new int[capacity];

            for (var i = 0; i < capacity; i++)
            {
                _positions[i] = -1;
            }
        }

        public int Count => _count;

        /// <summary>
        /// Empties the queue and grows its reusable storage when required.
        /// </summary>
        public void Reset(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            for (var i = 0; i < _count; i++)
            {
                _positions[_heap[i]] = -1;
            }

            _count = 0;
            if (capacity <= _heap.Length)
            {
                return;
            }

            var previousCapacity = _heap.Length;
            Array.Resize(ref _heap, capacity);
            Array.Resize(ref _positions, capacity);
            Array.Resize(ref _totalCosts, capacity);
            Array.Resize(ref _heuristicCosts, capacity);

            for (var i = previousCapacity; i < capacity; i++)
            {
                _positions[i] = -1;
            }
        }

        public void EnqueueOrDecrease(int cellIndex, int totalCost, int heuristicCost)
        {
            var currentPosition = _positions[cellIndex];
            if (currentPosition < 0)
            {
                _totalCosts[cellIndex] = totalCost;
                _heuristicCosts[cellIndex] = heuristicCost;
                _heap[_count] = cellIndex;
                _positions[cellIndex] = _count;
                SiftUp(_count);
                _count++;
                return;
            }

            if (ComparePriority(
                    totalCost,
                    heuristicCost,
                    cellIndex,
                    _totalCosts[cellIndex],
                    _heuristicCosts[cellIndex],
                    cellIndex) >= 0)
            {
                return;
            }

            _totalCosts[cellIndex] = totalCost;
            _heuristicCosts[cellIndex] = heuristicCost;
            SiftUp(currentPosition);
        }

        public int Dequeue()
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Cannot dequeue from an empty priority queue.");
            }

            var result = _heap[0];
            _positions[result] = -1;
            _count--;

            if (_count > 0)
            {
                var last = _heap[_count];
                _heap[0] = last;
                _positions[last] = 0;
                SiftDown(0);
            }

            return result;
        }

        private void SiftUp(int position)
        {
            while (position > 0)
            {
                var parent = (position - 1) / 2;
                if (!HasHigherPriority(_heap[position], _heap[parent]))
                {
                    return;
                }

                Swap(position, parent);
                position = parent;
            }
        }

        private void SiftDown(int position)
        {
            while (true)
            {
                var left = position * 2 + 1;
                if (left >= _count)
                {
                    return;
                }

                var right = left + 1;
                var best = right < _count && HasHigherPriority(_heap[right], _heap[left])
                    ? right
                    : left;

                if (!HasHigherPriority(_heap[best], _heap[position]))
                {
                    return;
                }

                Swap(position, best);
                position = best;
            }
        }

        private bool HasHigherPriority(int leftCellIndex, int rightCellIndex) =>
            ComparePriority(
                _totalCosts[leftCellIndex],
                _heuristicCosts[leftCellIndex],
                leftCellIndex,
                _totalCosts[rightCellIndex],
                _heuristicCosts[rightCellIndex],
                rightCellIndex) < 0;

        private static int ComparePriority(
            int leftTotalCost,
            int leftHeuristicCost,
            int leftCellIndex,
            int rightTotalCost,
            int rightHeuristicCost,
            int rightCellIndex)
        {
            var totalComparison = leftTotalCost.CompareTo(rightTotalCost);
            if (totalComparison != 0)
            {
                return totalComparison;
            }

            var heuristicComparison = leftHeuristicCost.CompareTo(rightHeuristicCost);
            return heuristicComparison != 0
                ? heuristicComparison
                : leftCellIndex.CompareTo(rightCellIndex);
        }

        private void Swap(int leftPosition, int rightPosition)
        {
            var leftCell = _heap[leftPosition];
            var rightCell = _heap[rightPosition];
            _heap[leftPosition] = rightCell;
            _heap[rightPosition] = leftCell;
            _positions[leftCell] = rightPosition;
            _positions[rightCell] = leftPosition;
        }
    }
}
