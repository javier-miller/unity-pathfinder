using System;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// A* implementation over an already-built grid. It has no scene, physics or
    /// MonoBehaviour dependencies.
    /// </summary>
    public sealed class GridPathfinder
    {
        private const int StraightCost = 10;
        private const int DiagonalCost = 14;
        private const byte Open = 1;
        private const byte Closed = 2;

        private static readonly int[] NeighbourX = { -1, 1, 0, 0, -1, -1, 1, 1 };
        private static readonly int[] NeighbourY = { 0, 0, -1, 1, -1, 1, -1, 1 };

        private readonly object _searchLock = new object();
        private readonly PathSearchContext _searchContext = new PathSearchContext();

        /// <summary>
        /// Finds a path between two grid coordinates.
        /// </summary>
        public PathResult FindPath(
            Grid grid,
            Vector2Int startCoordinates,
            Vector2Int destinationCoordinates,
            Vector3 requestedDestination,
            PathQueryOptions options = null)
        {
            var gridVersion = grid?.Version ?? 0;
            return FindPathCore(
                    grid,
                    startCoordinates,
                    destinationCoordinates,
                    requestedDestination,
                    options)
                .WithGridVersion(gridVersion);
        }

        private PathResult FindPathCore(
            Grid grid,
            Vector2Int startCoordinates,
            Vector2Int destinationCoordinates,
            Vector3 requestedDestination,
            PathQueryOptions options)
        {
            var queryOptions = options?.Clone() ?? PathQueryOptions.Default;
            var agentRadius = queryOptions.AgentProfile.GetSanitizedRadius();
            if (grid == null || grid.CellCount == 0)
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    requestedDestination);
            }

            if (!grid.Contains(startCoordinates.x, startCoordinates.y))
            {
                return PathResult.CreateFailure(
                    PathStatus.StartOutsideGrid,
                    requestedDestination);
            }

            var startCell = grid.GetCell(startCoordinates.x, startCoordinates.y);
            if (startCell == null)
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    requestedDestination);
            }

            if (!grid.HasClearance(startCoordinates, agentRadius))
            {
                return PathResult.CreateFailure(
                    PathStatus.StartBlocked,
                    requestedDestination);
            }

            var destinationInside = grid.Contains(destinationCoordinates.x, destinationCoordinates.y);
            var destinationCell = destinationInside
                ? grid.GetCell(destinationCoordinates.x, destinationCoordinates.y)
                : null;

            if (!queryOptions.FindNearestReachableDestination)
            {
                if (!destinationInside)
                {
                    return PathResult.CreateFailure(
                        PathStatus.DestinationOutsideGrid,
                        requestedDestination);
                }

                if (destinationCell == null)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination);
                }

                if (!grid.HasClearance(destinationCoordinates, agentRadius))
                {
                    return PathResult.CreateFailure(
                        PathStatus.DestinationBlocked,
                        requestedDestination);
                }
            }

            var exactDestinationAvailable =
                destinationInside &&
                destinationCell != null &&
                grid.HasClearance(destinationCoordinates, agentRadius);

            if (exactDestinationAvailable && startCoordinates == destinationCoordinates)
            {
                return PathResult.CreateAlreadyAtDestination(requestedDestination);
            }

            // The reusable context has exclusive ownership during a search. Calls
            // made concurrently on this instance are intentionally serialized.
            lock (_searchLock)
            {
                _searchContext.Begin(grid.CellCount);
                return Search(
                    grid,
                    startCoordinates,
                    destinationCoordinates,
                    requestedDestination,
                    exactDestinationAvailable,
                    queryOptions,
                    _searchContext);
            }
        }

        private static PathResult Search(
            Grid grid,
            Vector2Int startCoordinates,
            Vector2Int destinationCoordinates,
            Vector3 requestedDestination,
            bool exactDestinationAvailable,
            PathQueryOptions options,
            PathSearchContext context)
        {
            var walkingCosts = context.WalkingCosts;
            var parents = context.Parents;
            var states = context.States;
            var openQueue = context.OpenQueue;

            var startIndex = grid.GetIndex(startCoordinates.x, startCoordinates.y);
            var exactDestinationIndex = exactDestinationAvailable
                ? grid.GetIndex(destinationCoordinates.x, destinationCoordinates.y)
                : -1;

            context.InitializeCell(startIndex);
            walkingCosts[startIndex] = 0;
            states[startIndex] = Open;
            var startHeuristic = CalculateDistanceCost(
                startCoordinates.x,
                startCoordinates.y,
                destinationCoordinates.x,
                destinationCoordinates.y,
                options.AllowDiagonalMovement);
            openQueue.EnqueueOrDecrease(startIndex, startHeuristic, startHeuristic);

            var bestReachableIndex = startIndex;
            var bestDistance = CalculateSquaredDistance(startCoordinates, destinationCoordinates);
            var expandedNodeCount = 0;

            while (openQueue.Count > 0)
            {
                if (options.MaxExpandedNodes > 0 &&
                    expandedNodeCount >= options.MaxExpandedNodes)
                {
                    return PathResult.CreateFailure(
                        PathStatus.SearchLimitReached,
                        requestedDestination,
                        expandedNodeCount);
                }

                var currentIndex = openQueue.Dequeue();
                states[currentIndex] = Closed;
                expandedNodeCount++;

                var currentCell = grid.GetCellByIndex(currentIndex);
                if (currentCell == null)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination,
                        expandedNodeCount);
                }

                var currentCoordinates = currentCell.Coordinates;
                if (options.FindNearestReachableDestination)
                {
                    UpdateBestReachable(
                        currentIndex,
                        currentCoordinates,
                        destinationCoordinates,
                        walkingCosts,
                        ref bestReachableIndex,
                        ref bestDistance);
                }

                if (exactDestinationAvailable && currentIndex == exactDestinationIndex)
                {
                    return CreateSuccessResult(
                        grid,
                        startIndex,
                        currentIndex,
                        parents,
                        requestedDestination,
                        requestedDestination,
                        false,
                        expandedNodeCount,
                        options,
                        context);
                }

                VisitNeighbours(
                    grid,
                    currentIndex,
                    currentCoordinates,
                    destinationCoordinates,
                    options,
                    walkingCosts,
                    parents,
                    states,
                    openQueue,
                    context);
            }

            if (!options.FindNearestReachableDestination)
            {
                return PathResult.CreateFailure(
                    PathStatus.Unreachable,
                    requestedDestination,
                    expandedNodeCount);
            }

            var resolvedCell = grid.GetCellByIndex(bestReachableIndex);
            if (resolvedCell == null)
            {
                return PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    requestedDestination,
                    expandedNodeCount);
            }

            return CreateSuccessResult(
                grid,
                startIndex,
                bestReachableIndex,
                parents,
                requestedDestination,
                resolvedCell.WorldPosition,
                true,
                expandedNodeCount,
                options,
                context);
        }

        private static void VisitNeighbours(
            Grid grid,
            int currentIndex,
            Vector2Int currentCoordinates,
            Vector2Int destinationCoordinates,
            PathQueryOptions options,
            int[] walkingCosts,
            int[] parents,
            byte[] states,
            PathPriorityQueue openQueue,
            PathSearchContext context)
        {
            var neighbourCount = options.AllowDiagonalMovement ? NeighbourX.Length : 4;
            for (var i = 0; i < neighbourCount; i++)
            {
                var offsetX = NeighbourX[i];
                var offsetY = NeighbourY[i];
                var neighbourX = currentCoordinates.x + offsetX;
                var neighbourY = currentCoordinates.y + offsetY;
                var neighbourCell = grid.GetCell(neighbourX, neighbourY);

                var agentRadius = options.AgentProfile.GetSanitizedRadius();
                if (neighbourCell == null ||
                    !grid.HasClearance(
                        new Vector2Int(neighbourX, neighbourY),
                        agentRadius))
                {
                    continue;
                }

                var isDiagonal = offsetX != 0 && offsetY != 0;
                if (isDiagonal && options.PreventCornerCutting &&
                    !CanTraverseDiagonal(
                        grid,
                        currentCoordinates,
                        offsetX,
                        offsetY,
                        agentRadius))
                {
                    continue;
                }

                var neighbourIndex = grid.GetIndex(neighbourX, neighbourY);
                context.InitializeCell(neighbourIndex);
                if (states[neighbourIndex] == Closed)
                {
                    continue;
                }

                var baseMovementCost = isDiagonal ? DiagonalCost : StraightCost;
                var movementCost = SaturatingMultiply(
                    baseMovementCost,
                    neighbourCell.TraversalCost);
                var tentativeWalkingCost = SaturatingAdd(walkingCosts[currentIndex], movementCost);
                if (tentativeWalkingCost >= walkingCosts[neighbourIndex])
                {
                    continue;
                }

                walkingCosts[neighbourIndex] = tentativeWalkingCost;
                parents[neighbourIndex] = currentIndex;

                var heuristicCost = CalculateDistanceCost(
                    neighbourX,
                    neighbourY,
                    destinationCoordinates.x,
                    destinationCoordinates.y,
                    options.AllowDiagonalMovement);
                var totalCost = SaturatingAdd(tentativeWalkingCost, heuristicCost);

                states[neighbourIndex] = Open;
                openQueue.EnqueueOrDecrease(neighbourIndex, totalCost, heuristicCost);
            }
        }

        private static bool CanTraverseDiagonal(
            Grid grid,
            Vector2Int currentCoordinates,
            int offsetX,
            int offsetY,
            float agentRadius)
        {
            return grid.HasClearance(
                       new Vector2Int(
                           currentCoordinates.x + offsetX,
                           currentCoordinates.y),
                       agentRadius) &&
                   grid.HasClearance(
                       new Vector2Int(
                           currentCoordinates.x,
                           currentCoordinates.y + offsetY),
                       agentRadius);
        }

        private static PathResult CreateSuccessResult(
            Grid grid,
            int startIndex,
            int destinationIndex,
            int[] parents,
            Vector3 requestedDestination,
            Vector3 resolvedDestination,
            bool usedNearestReachableDestination,
            int expandedNodeCount,
            PathQueryOptions options,
            PathSearchContext context)
        {
            var waypointCount = 0;
            var currentIndex = destinationIndex;

            while (currentIndex != startIndex)
            {
                var currentCell = grid.GetCellByIndex(currentIndex);
                if (currentCell == null || parents[currentIndex] < 0)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination,
                        expandedNodeCount);
                }

                waypointCount++;
                if (waypointCount > grid.CellCount)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination,
                        expandedNodeCount);
                }

                currentIndex = parents[currentIndex];
            }

            var pathLength = waypointCount + 1;
            var pathIndices = context.PathIndices;
            pathIndices[0] = startIndex;
            currentIndex = destinationIndex;
            for (var i = pathLength - 1; i > 0; i--)
            {
                var currentCell = grid.GetCellByIndex(currentIndex);
                if (currentCell == null)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination,
                        expandedNodeCount);
                }

                pathIndices[i] = currentIndex;
                currentIndex = parents[currentIndex];
            }

            var firstResultIndex = 1;
            var resultWaypointCount = waypointCount;
            if (options.SmoothPath &&
                !grid.HasWeightedTerrain &&
                waypointCount > 1)
            {
                resultWaypointCount = SmoothPath(
                    grid,
                    pathIndices,
                    pathLength,
                    options.AllowDiagonalMovement,
                    options.PreventCornerCutting,
                    options.AgentProfile.GetSanitizedRadius());
                firstResultIndex = 0;
            }

            var waypoints = new Vector3[resultWaypointCount];
            for (var i = 0; i < resultWaypointCount; i++)
            {
                var cell = grid.GetCellByIndex(pathIndices[firstResultIndex + i]);
                if (cell == null)
                {
                    return PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        requestedDestination,
                        expandedNodeCount);
                }

                waypoints[i] = cell.WorldPosition;
            }

            return PathResult.CreateSuccessOwned(
                waypoints,
                requestedDestination,
                resolvedDestination,
                usedNearestReachableDestination,
                expandedNodeCount,
                context.WalkingCosts[destinationIndex]);
        }

        /// <summary>
        /// Greedily keeps the furthest original waypoint visible from each anchor.
        /// Selected destination indices are compacted into the beginning of the buffer.
        /// </summary>
        private static int SmoothPath(
            Grid grid,
            int[] pathIndices,
            int pathLength,
            bool allowDiagonalMovement,
            bool preventCornerCutting,
            float agentRadius)
        {
            var smoothedCount = 0;
            var anchorPosition = 0;
            var anchorIndex = pathIndices[0];

            while (anchorPosition < pathLength - 1)
            {
                var selectedPosition = anchorPosition + 1;
                for (var candidatePosition = pathLength - 1;
                     candidatePosition > anchorPosition;
                     candidatePosition--)
                {
                    if (HasLineOfSight(
                            grid,
                            anchorIndex,
                            pathIndices[candidatePosition],
                            allowDiagonalMovement,
                            preventCornerCutting,
                            agentRadius))
                    {
                        selectedPosition = candidatePosition;
                        break;
                    }
                }

                var selectedIndex = pathIndices[selectedPosition];
                pathIndices[smoothedCount] = selectedIndex;
                smoothedCount++;
                anchorPosition = selectedPosition;
                anchorIndex = selectedIndex;
            }

            return smoothedCount;
        }

        /// <summary>
        /// Traverses every grid cell touched by a segment between two cell centres.
        /// Exact corner crossings optionally require both adjacent orthogonal cells.
        /// </summary>
        internal static bool HasLineOfSight(
            Grid grid,
            int fromIndex,
            int toIndex,
            bool allowDiagonalMovement,
            bool preventCornerCutting,
            float agentRadius = 0f)
        {
            var fromCell = grid.GetCellByIndex(fromIndex);
            var toCell = grid.GetCellByIndex(toIndex);
            if (fromCell == null ||
                toCell == null ||
                !grid.HasClearance(fromCell.Coordinates, agentRadius) ||
                !grid.HasClearance(toCell.Coordinates, agentRadius))
            {
                return false;
            }

            var from = fromCell.Coordinates;
            var to = toCell.Coordinates;
            var deltaX = to.x - from.x;
            var deltaY = to.y - from.y;
            if (!allowDiagonalMovement && deltaX != 0 && deltaY != 0)
            {
                return false;
            }

            var stepX = Math.Sign(deltaX);
            var stepY = Math.Sign(deltaY);
            var horizontalSteps = Math.Abs(deltaX);
            var verticalSteps = Math.Abs(deltaY);
            var completedHorizontalSteps = 0;
            var completedVerticalSteps = 0;
            var currentX = from.x;
            var currentY = from.y;

            while (completedHorizontalSteps < horizontalSteps ||
                   completedVerticalSteps < verticalSteps)
            {
                var decision =
                    (1L + 2L * completedHorizontalSteps) * verticalSteps -
                    (1L + 2L * completedVerticalSteps) * horizontalSteps;

                if (decision == 0)
                {
                    if (preventCornerCutting &&
                        (!IsWalkable(
                             grid,
                             currentX + stepX,
                             currentY,
                             agentRadius) ||
                         !IsWalkable(
                             grid,
                             currentX,
                             currentY + stepY,
                             agentRadius)))
                    {
                        return false;
                    }

                    currentX += stepX;
                    currentY += stepY;
                    completedHorizontalSteps++;
                    completedVerticalSteps++;
                }
                else if (decision < 0)
                {
                    currentX += stepX;
                    completedHorizontalSteps++;
                }
                else
                {
                    currentY += stepY;
                    completedVerticalSteps++;
                }

                if (!IsWalkable(grid, currentX, currentY, agentRadius))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool HasLineOfSight(
            Grid grid,
            Vector2Int from,
            Vector2Int to,
            bool allowDiagonalMovement,
            bool preventCornerCutting,
            float agentRadius = 0f)
        {
            if (grid == null ||
                !grid.Contains(from.x, from.y) ||
                !grid.Contains(to.x, to.y))
            {
                return false;
            }

            return HasLineOfSight(
                grid,
                grid.GetIndex(from.x, from.y),
                grid.GetIndex(to.x, to.y),
                allowDiagonalMovement,
                preventCornerCutting,
                agentRadius);
        }

        private static bool IsWalkable(
            Grid grid,
            int x,
            int y,
            float agentRadius) =>
            grid.HasClearance(new Vector2Int(x, y), agentRadius);

        private static void UpdateBestReachable(
            int currentIndex,
            Vector2Int currentCoordinates,
            Vector2Int destinationCoordinates,
            int[] walkingCosts,
            ref int bestReachableIndex,
            ref double bestDistance)
        {
            var currentDistance = CalculateSquaredDistance(currentCoordinates, destinationCoordinates);
            if (currentDistance < bestDistance ||
                (currentDistance.Equals(bestDistance) &&
                 (walkingCosts[currentIndex] < walkingCosts[bestReachableIndex] ||
                  (walkingCosts[currentIndex] == walkingCosts[bestReachableIndex] &&
                   currentIndex < bestReachableIndex))))
            {
                bestReachableIndex = currentIndex;
                bestDistance = currentDistance;
            }
        }

        private static double CalculateSquaredDistance(Vector2Int a, Vector2Int b)
        {
            var deltaX = (double)a.x - b.x;
            var deltaY = (double)a.y - b.y;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static int CalculateDistanceCost(
            int fromX,
            int fromY,
            int toX,
            int toY,
            bool allowDiagonalMovement)
        {
            var xDistance = Math.Abs((long)fromX - toX);
            var yDistance = Math.Abs((long)fromY - toY);
            long cost;

            if (allowDiagonalMovement)
            {
                var diagonalSteps = Math.Min(xDistance, yDistance);
                var straightSteps = Math.Abs(xDistance - yDistance);
                cost = DiagonalCost * diagonalSteps + StraightCost * straightSteps;
            }
            else
            {
                cost = StraightCost * (xDistance + yDistance);
            }

            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (left == int.MaxValue || right > int.MaxValue - left)
            {
                return int.MaxValue;
            }

            return left + right;
        }

        private static int SaturatingMultiply(int left, int right)
        {
            if (left <= 0 || right <= 0)
            {
                return 0;
            }

            return left > int.MaxValue / right
                ? int.MaxValue
                : left * right;
        }
    }
}
