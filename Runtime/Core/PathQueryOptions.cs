using System;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Configures a path query independently from movement and presentation concerns.
    /// </summary>
    public sealed class PathQueryOptions
    {
        private int _maxExpandedNodes;
        private PathAgentProfile _agentProfile = new PathAgentProfile();

        /// <summary>
        /// Gets a new options instance containing the package defaults.
        /// </summary>
        public static PathQueryOptions Default => new PathQueryOptions();

        /// <summary>
        /// Gets or sets whether the path may use diagonal grid connections.
        /// </summary>
        public bool AllowDiagonalMovement { get; set; } = true;

        /// <summary>
        /// Gets or sets whether diagonal movement through blocked corners is forbidden.
        /// Ignored when diagonal movement is disabled.
        /// </summary>
        public bool PreventCornerCutting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether an unreachable destination may be replaced by the nearest
        /// reachable cell.
        /// </summary>
        public bool FindNearestReachableDestination { get; set; }

        /// <summary>
        /// Gets or sets whether redundant waypoints should be removed using grid
        /// line-of-sight checks after A* succeeds. Weighted paths are intentionally
        /// left unsmoothed so a shortcut cannot ignore terrain costs.
        /// </summary>
        public bool SmoothPath { get; set; }

        /// <summary>
        /// Gets or sets the clearance profile used by walkability and line-of-sight
        /// checks. Null is normalized to a point-agent profile.
        /// </summary>
        public PathAgentProfile AgentProfile
        {
            get => _agentProfile;
            set => _agentProfile = value ?? new PathAgentProfile();
        }

        /// <summary>
        /// Gets or sets the optional maximum number of nodes expanded by a query.
        /// A value of zero means that no node-count limit is requested.
        /// </summary>
        public int MaxExpandedNodes
        {
            get => _maxExpandedNodes;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Maximum expanded nodes cannot be negative.");
                }

                _maxExpandedNodes = value;
            }
        }

        /// <summary>
        /// Creates a copy that can be safely captured when queuing a request.
        /// </summary>
        public PathQueryOptions Clone() => new PathQueryOptions
        {
            AllowDiagonalMovement = AllowDiagonalMovement,
            PreventCornerCutting = PreventCornerCutting,
            FindNearestReachableDestination = FindNearestReachableDestination,
            SmoothPath = SmoothPath,
            AgentProfile = AgentProfile.Clone(),
            MaxExpandedNodes = MaxExpandedNodes
        };
    }
}
