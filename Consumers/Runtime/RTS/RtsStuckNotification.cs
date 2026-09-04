using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Immutable diagnostic emitted when an RTS unit makes insufficient progress.
    /// </summary>
    public readonly struct RtsStuckNotification
    {
        internal RtsStuckNotification(
            int operationId,
            Vector2 position,
            float observedProgress,
            int consecutiveStuckWindows,
            int recoveryAttemptCount,
            bool repathAccepted,
            bool recoveryExhausted)
        {
            OperationId = operationId;
            Position = position;
            ObservedProgress = observedProgress;
            ConsecutiveStuckWindows = consecutiveStuckWindows;
            RecoveryAttemptCount = recoveryAttemptCount;
            RepathAccepted = repathAccepted;
            RecoveryExhausted = recoveryExhausted;
        }

        public int OperationId { get; }

        public Vector2 Position { get; }

        public float ObservedProgress { get; }

        public int ConsecutiveStuckWindows { get; }

        public int RecoveryAttemptCount { get; }

        public bool RepathAccepted { get; }

        public bool RecoveryExhausted { get; }
    }
}
