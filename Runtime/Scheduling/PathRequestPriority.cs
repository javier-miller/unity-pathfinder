namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Determines which queued path requests are processed first.
    /// Requests with the same priority retain FIFO ordering.
    /// </summary>
    public enum PathRequestPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
