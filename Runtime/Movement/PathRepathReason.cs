namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Explains why an active movement requested a replacement route.
    /// </summary>
    public enum PathRepathReason
    {
        None = 0,
        GridInvalidated = 1,
        StuckRecovery = 2,
        Manual = 3
    }
}
