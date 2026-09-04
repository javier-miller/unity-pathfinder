using System;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Describes the physical clearance required by one path query.
    /// </summary>
    [Serializable]
    public sealed class PathAgentProfile
    {
        [SerializeField]
        [Min(0f)]
        private float radius;

        public PathAgentProfile(float radius = 0f)
        {
            Radius = radius;
        }

        /// <summary>
        /// Gets or sets the conservative world-space radius required around a cell.
        /// Zero preserves point-agent behaviour.
        /// </summary>
        public float Radius
        {
            get => radius;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Agent radius must be finite and non-negative.");
                }

                radius = value;
            }
        }

        public PathAgentProfile Clone() =>
            new PathAgentProfile(GetSanitizedRadius());

        internal float GetSanitizedRadius() =>
            float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f
                ? 0f
                : radius;

        internal void Sanitize()
        {
            radius = GetSanitizedRadius();
        }
    }
}
