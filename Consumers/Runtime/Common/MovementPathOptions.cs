using System;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Serializable inspector representation of <see cref="PathQueryOptions"/>.
    /// A fresh query-options instance is created for every movement order.
    /// </summary>
    [Serializable]
    public sealed class MovementPathOptions
    {
        [SerializeField]
        private bool allowDiagonalMovement = true;

        [SerializeField]
        private bool preventCornerCutting = true;

        [SerializeField]
        private bool findNearestReachableDestination;

        [SerializeField]
        private bool smoothPath;

        [SerializeField]
        private PathAgentProfile agentProfile = new PathAgentProfile();

        [SerializeField]
        [Min(0)]
        private int maxExpandedNodes;

        public MovementPathOptions(
            bool findNearestReachableDestination = false,
            bool smoothPath = false)
        {
            this.findNearestReachableDestination = findNearestReachableDestination;
            this.smoothPath = smoothPath;
        }

        public bool AllowDiagonalMovement
        {
            get => allowDiagonalMovement;
            set => allowDiagonalMovement = value;
        }

        public bool PreventCornerCutting
        {
            get => preventCornerCutting;
            set => preventCornerCutting = value;
        }

        public bool FindNearestReachableDestination
        {
            get => findNearestReachableDestination;
            set => findNearestReachableDestination = value;
        }

        public bool SmoothPath
        {
            get => smoothPath;
            set => smoothPath = value;
        }

        public PathAgentProfile AgentProfile
        {
            get
            {
                agentProfile ??= new PathAgentProfile();
                return agentProfile;
            }
        }

        public int MaxExpandedNodes
        {
            get => maxExpandedNodes;
            set => maxExpandedNodes = Mathf.Max(0, value);
        }

        /// <summary>
        /// Creates an independent options object for one path query.
        /// </summary>
        public PathQueryOptions CreateQueryOptions() => new PathQueryOptions
        {
            AllowDiagonalMovement = allowDiagonalMovement,
            PreventCornerCutting = preventCornerCutting,
            FindNearestReachableDestination = findNearestReachableDestination,
            SmoothPath = smoothPath,
            AgentProfile = AgentProfile.Clone(),
            MaxExpandedNodes = Mathf.Max(0, maxExpandedNodes)
        };
    }
}
