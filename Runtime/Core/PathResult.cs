using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Describes the outcome of a path query without relying on ambiguous boolean values.
    /// </summary>
    public enum PathStatus
    {
        Undefined = 0,
        Success = 1,
        SuccessNearestReachable = 2,
        AlreadyAtDestination = 3,
        StartOutsideGrid = 4,
        DestinationOutsideGrid = 5,
        StartBlocked = 6,
        DestinationBlocked = 7,
        Unreachable = 8,
        SearchLimitReached = 9,
        Cancelled = 10,
        InvalidConfiguration = 11
    }

    /// <summary>
    /// Immutable result returned by the detailed pathfinding API.
    /// </summary>
    public sealed class PathResult
    {
        private static readonly ReadOnlyCollection<Vector3> EmptyWaypoints =
            Array.AsReadOnly(Array.Empty<Vector3>());

        private readonly ReadOnlyCollection<Vector3> _waypoints;

        private PathResult(
            PathStatus status,
            ReadOnlyCollection<Vector3> waypoints,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            bool hasResolvedDestination,
            int expandedNodeCount,
            int totalCost = 0,
            long gridVersion = 0)
        {
            if (expandedNodeCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expandedNodeCount),
                    "Expanded node count cannot be negative.");
            }

            if (gridVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gridVersion),
                    "Grid version cannot be negative.");
            }

            if (totalCost < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalCost),
                    "Total path cost cannot be negative.");
            }

            Status = status;
            RequestedDestination = requestedDestination;
            ResolvedDestination = resolvedDestination;
            HasResolvedDestination = hasResolvedDestination;
            ExpandedNodeCount = expandedNodeCount;
            TotalCost = totalCost;
            GridVersion = gridVersion;
            _waypoints = waypoints ?? EmptyWaypoints;
        }

        /// <summary>
        /// Gets the status that explains how the query finished.
        /// </summary>
        public PathStatus Status { get; }

        /// <summary>
        /// Gets whether the result represents a usable destination.
        /// </summary>
        public bool Succeeded => IsSuccessStatus(Status);

        /// <summary>
        /// Gets the ordered waypoints after the starting position.
        /// </summary>
        public IReadOnlyList<Vector3> Waypoints => _waypoints;

        /// <summary>
        /// Gets the destination originally requested by the caller.
        /// </summary>
        public Vector3 RequestedDestination { get; }

        /// <summary>
        /// Gets the destination selected by navigation when one was resolved.
        /// </summary>
        public Vector3 ResolvedDestination { get; }

        /// <summary>
        /// Gets whether <see cref="ResolvedDestination"/> contains a meaningful value.
        /// </summary>
        public bool HasResolvedDestination { get; }

        /// <summary>
        /// Gets the number of nodes expanded while processing the query.
        /// </summary>
        public int ExpandedNodeCount { get; }

        /// <summary>
        /// Gets the accumulated A* movement cost before optional smoothing.
        /// </summary>
        public int TotalCost { get; }

        /// <summary>
        /// Gets the navigation-grid version used by the query, or zero when the
        /// caller failed before a grid was available.
        /// </summary>
        public long GridVersion { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static PathResult CreateSuccess(
            IEnumerable<Vector3> waypoints,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            bool usedNearestReachableDestination = false,
            int expandedNodeCount = 0,
            int totalCost = 0)
        {
            var status = usedNearestReachableDestination
                ? PathStatus.SuccessNearestReachable
                : PathStatus.Success;

            return new PathResult(
                status,
                CopyWaypoints(waypoints),
                requestedDestination,
                resolvedDestination,
                true,
                expandedNodeCount,
                totalCost);
        }

        /// <summary>
        /// Creates a successful result from an array whose ownership is transferred
        /// to the result. The array must not be retained or modified by the caller.
        /// </summary>
        internal static PathResult CreateSuccessOwned(
            Vector3[] waypoints,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            bool usedNearestReachableDestination,
            int expandedNodeCount,
            int totalCost)
        {
            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            var status = usedNearestReachableDestination
                ? PathStatus.SuccessNearestReachable
                : PathStatus.Success;

            return new PathResult(
                status,
                Array.AsReadOnly(waypoints),
                requestedDestination,
                resolvedDestination,
                true,
                expandedNodeCount,
                totalCost);
        }

        /// <summary>
        /// Creates a successful result for a query whose agent is already at its destination.
        /// </summary>
        public static PathResult CreateAlreadyAtDestination(
            Vector3 destination,
            int expandedNodeCount = 0) =>
            new PathResult(
                PathStatus.AlreadyAtDestination,
                null,
                destination,
                destination,
                true,
                expandedNodeCount);

        /// <summary>
        /// Creates an unsuccessful result with no resolved destination.
        /// </summary>
        public static PathResult CreateFailure(
            PathStatus status,
            Vector3 requestedDestination,
            int expandedNodeCount = 0)
        {
            if (status == PathStatus.Undefined)
            {
                throw new ArgumentException(
                    "Undefined is not a valid completed path status.",
                    nameof(status));
            }

            if (IsSuccessStatus(status))
            {
                throw new ArgumentException(
                    "A successful status cannot be used to create a failure result.",
                    nameof(status));
            }

            return new PathResult(
                status,
                null,
                requestedDestination,
                default,
                false,
                expandedNodeCount);
        }

        private static bool IsSuccessStatus(PathStatus status) =>
            status == PathStatus.Success ||
            status == PathStatus.SuccessNearestReachable ||
            status == PathStatus.AlreadyAtDestination;

        private static ReadOnlyCollection<Vector3> CopyWaypoints(
            IEnumerable<Vector3> waypoints) =>
            waypoints == null
                ? EmptyWaypoints
                : new List<Vector3>(waypoints).AsReadOnly();

        internal PathResult WithGridVersion(long gridVersion)
        {
            if (GridVersion == gridVersion)
            {
                return this;
            }

            return new PathResult(
                Status,
                _waypoints,
                RequestedDestination,
                ResolvedDestination,
                HasResolvedDestination,
                ExpandedNodeCount,
                TotalCost,
                gridVersion);
        }
    }
}
