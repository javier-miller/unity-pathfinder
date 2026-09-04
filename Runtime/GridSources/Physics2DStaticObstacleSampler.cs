using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Samples only the Physics2D layers classified as static navigation obstacles.
    /// </summary>
    public sealed class Physics2DStaticObstacleSampler : IGridCellObstacleSampler
    {
        public Physics2DStaticObstacleSampler(LayerMask staticObstacleMask)
        {
            StaticObstacleMask = staticObstacleMask;
        }

        public LayerMask StaticObstacleMask { get; }

        public bool IsBlocked(Vector3 worldCenter, Vector2 worldSize) =>
            Physics2D.OverlapBox(
                worldCenter,
                worldSize,
                0f,
                StaticObstacleMask) != null;
    }
}
