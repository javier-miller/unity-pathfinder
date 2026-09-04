using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Optional main-thread steering hook applied to the velocity requested by
    /// <see cref="PathfinderMovement"/> during a physics step.
    /// </summary>
    public interface IPathfinderMovementVelocityModifier
    {
        /// <summary>
        /// Gets the execution order. Lower values execute first; equal values keep
        /// registration order. The value must remain stable while registered.
        /// </summary>
        int VelocityModifierOrder { get; }

        /// <summary>
        /// Returns a modified world-space velocity. Implementations should remain
        /// allocation-free because this method is called from FixedUpdate.
        /// </summary>
        Vector2 ModifyVelocity(
            PathfinderMovement movement,
            Vector2 desiredVelocity,
            float fixedDeltaTime);
    }

    /// <summary>
    /// Conventional execution stages used by the included movement consumers.
    /// Custom modifiers may use intermediate values when required.
    /// </summary>
    public static class PathfinderMovementVelocityModifierOrder
    {
        public const int Default = 0;

        /// <summary>
        /// Local influences that adjust the desired travel direction.
        /// </summary>
        public const int LocalAvoidance = 100;

        /// <summary>
        /// Locomotion constraints applied to the resulting direction, such as
        /// a vehicle's maximum turn rate.
        /// </summary>
        public const int LocomotionConstraint = 200;
    }
}
