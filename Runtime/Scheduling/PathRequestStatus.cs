namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Describes the lifecycle of a scheduled path request.
    /// </summary>
    public enum PathRequestStatus
    {
        Queued = 0,
        Running = 1,
        Completed = 2,
        Cancelled = 3
    }
}
