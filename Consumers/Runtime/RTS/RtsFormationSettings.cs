using System;
using UnityEngine;

namespace SparkyGames.Pathfinder.Consumers
{
    /// <summary>
    /// Serializable policy used when generating and resolving RTS formation slots.
    /// </summary>
    [Serializable]
    public sealed class RtsFormationSettings
    {
        private const float MinimumDistance = 0.01f;

        [SerializeField]
        [Min(MinimumDistance)]
        private float spacing = 1.5f;

        [SerializeField]
        [Min(0)]
        [Tooltip("Zero chooses a near-square number of columns automatically.")]
        private int columns;

        [SerializeField]
        [Min(1)]
        private int maximumCandidateAttemptsPerUnit = 12;

        [SerializeField]
        [Min(MinimumDistance)]
        private float candidateSearchStep = 1f;

        [SerializeField]
        [Min(MinimumDistance)]
        private float minimumResolvedSlotSeparation = 0.5f;

        [SerializeField]
        private bool findNearestReachableSlot = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum world distance from a requested candidate to an accepted nearest-reachable fallback. Zero means unlimited.")]
        private float maximumFallbackDistance = 2f;

        public float Spacing
        {
            get => spacing;
            set => spacing = Mathf.Max(MinimumDistance, value);
        }

        public int Columns
        {
            get => columns;
            set => columns = Mathf.Max(0, value);
        }

        public int MaximumCandidateAttemptsPerUnit
        {
            get => maximumCandidateAttemptsPerUnit;
            set => maximumCandidateAttemptsPerUnit = Mathf.Max(1, value);
        }

        public float CandidateSearchStep
        {
            get => candidateSearchStep;
            set => candidateSearchStep = Mathf.Max(MinimumDistance, value);
        }

        public float MinimumResolvedSlotSeparation
        {
            get => minimumResolvedSlotSeparation;
            set => minimumResolvedSlotSeparation =
                Mathf.Max(MinimumDistance, value);
        }

        public bool FindNearestReachableSlot
        {
            get => findNearestReachableSlot;
            set => findNearestReachableSlot = value;
        }

        /// <summary>
        /// Gets or sets how far a nearest-reachable result may be from the current
        /// requested candidate. Zero allows an unlimited fallback distance.
        /// </summary>
        public float MaximumFallbackDistance
        {
            get => maximumFallbackDistance;
            set => maximumFallbackDistance = Mathf.Max(0f, value);
        }

        internal void Sanitize()
        {
            Spacing = spacing;
            Columns = columns;
            MaximumCandidateAttemptsPerUnit = maximumCandidateAttemptsPerUnit;
            CandidateSearchStep = candidateSearchStep;
            MinimumResolvedSlotSeparation = minimumResolvedSlotSeparation;
            MaximumFallbackDistance = maximumFallbackDistance;
        }
    }
}
