namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Describes the lifecycle of one movement operation.
    /// </summary>
    public enum PathfinderMovementState
    {
        Idle = 0,
        WaitingForPath = 7,
        FollowingPath = 1,
        Paused = 2,
        Arrived = 3,
        Blocked = 4,
        Cancelled = 5,
        Failed = 6
    }
}
