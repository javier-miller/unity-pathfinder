using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Answers whether a cell footprint is blocked by obstacles that belong in a
    /// navigation snapshot. Dynamic obstacles and agents must not be sampled here.
    /// </summary>
    public interface IGridCellObstacleSampler
    {
        bool IsBlocked(Vector3 worldCenter, Vector2 worldSize);
    }
}
