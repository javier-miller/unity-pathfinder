using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    public abstract class Pathfinding : MonoBehaviour, IPathfinding
    {
        private const int MOVE_STRAIGHT_COST = 10;
        private const int MOVE_DIAGONAL_COST = 14;

        protected Grid _grid;

        /// <summary>
        /// The no walkable mask
        /// </summary>
        [SerializeField]
        protected LayerMask colliderMask;

        /// <summary>
        /// The show gizmos
        /// </summary>
        [SerializeField]
        protected bool showGizmos = true;

        /// <summary>
        /// Awakes this instance.
        /// </summary>
        protected virtual void Awake()
        {
            _grid = BuildGrid();
        }

        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        public virtual bool GetPath(Vector3 position, out Vector3 result)
        {
            result = default;
            var cell = GetCellPosition(position);

            var value = _grid.GetCell(cell.x, cell.y);
            if (value is null) return false;

            result = value.WorldPosition;
            return true;
        }

        /// <summary>
        /// Finds the path.
        /// </summary>
        /// <param name="startWorldPosition">The start world position.</param>
        /// <param name="endWorldPosition">The end world position.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public virtual bool FindPath(Vector3 startWorldPosition, Vector3 endWorldPosition, out IEnumerable<Vector3> path)
        {
            var start = GetCellPosition(startWorldPosition);
            var end = GetCellPosition(endWorldPosition);

            path = Enumerable.Empty<Vector3>();

            var findPath = FindPath(start.x, start.y, end.x, end.y);
            if (findPath == null) return false;

            var list = findPath.Select(pathNode => pathNode.Cell.WorldPosition).ToList();

            if (list.Any()) list.RemoveAt(0);

            path = list;

            return true;
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public virtual void Refresh()
        {
            _grid = BuildGrid();
        }

        /// <summary>
        /// Finds the path.
        /// </summary>
        /// <param name="startX">The start x.</param>
        /// <param name="startY">The start y.</param>
        /// <param name="endX">The end x.</param>
        /// <param name="endY">The end y.</param>
        /// <returns></returns>
        protected virtual IEnumerable<PathNode> FindPath(int startX, int startY, int endX, int endY)
        {
            var startCell = _grid.GetCell(startX, startY);
            var endCell = _grid.GetCell(endX, endY);

            if (startCell == null || endCell == null) return null;

            var startNode = startCell.CreatePathNode();
            var endNode = endCell.CreatePathNode();

            var openList = new Dictionary<GridCell, PathNode> { { startNode.Cell, startNode } };
            var closedList = new Dictionary<GridCell, PathNode>();

            startNode.SetCosts(0, CalculateDistanceCost(startNode, endNode));

            while (openList.Count > 0)
            {
                var currentNode = GetLowestFCostNode(openList);
                if (currentNode.Cell == endNode.Cell)
                {
                    return CalculatePath(currentNode);
                }

                openList.Remove(currentNode.Cell);
                closedList.Add(currentNode.Cell, currentNode);

                FindNeighbourNodes(currentNode, endNode, openList, closedList);
            }

            // Out of nodes on the openList
            return null;
        }

        /// <summary>
        /// Calculates the path.
        /// </summary>
        /// <param name="endNode">The end node.</param>
        /// <returns></returns>
        protected virtual List<PathNode> CalculatePath(PathNode endNode)
        {
            var path = new List<PathNode> { endNode };
            var currentNode = endNode;

            while (currentNode.SourceNode != null)
            {
                path.Add(currentNode.SourceNode);
                currentNode = currentNode.SourceNode;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Finds the neighbour nodes.
        /// </summary>
        /// <param name="currentNode">The current node.</param>
        /// <param name="endNode">The end node.</param>
        /// <param name="openList">The open list.</param>
        /// <param name="closedList">The closed list.</param>
        protected virtual void FindNeighbourNodes(PathNode currentNode, PathNode endNode, Dictionary<GridCell, PathNode> openList, Dictionary<GridCell, PathNode> closedList)
        {
            foreach (var neighbourNode in GetNeighbourList(currentNode))
            {
                if (closedList.ContainsKey(neighbourNode.Cell)) continue;
                if (!neighbourNode.Cell.IsWalkable)
                {
                    closedList.Add(neighbourNode.Cell, neighbourNode);
                    continue;
                }

                var tentativeGCost = currentNode.WalkingCost + CalculateDistanceCost(currentNode, neighbourNode);
                if (tentativeGCost < neighbourNode.WalkingCost)
                {
                    neighbourNode.SetSource(currentNode)
                    .SetCosts(tentativeGCost, CalculateDistanceCost(neighbourNode, endNode));

                    if (!openList.ContainsKey(neighbourNode.Cell)) openList.Add(neighbourNode.Cell, neighbourNode);
                }

                if (openList.Count > 200)
                {
                    openList.Clear();
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the neighbour list.
        /// </summary>
        /// <param name="currentNode">The current node.</param>
        /// <returns></returns>
        protected virtual List<PathNode> GetNeighbourList(PathNode currentNode)
        {
            var height = _grid.CellsCount.y;
            var width = _grid.CellsCount.x;

            var coordenateX = currentNode.Cell.Coordenates.x;
            var coordenateY = currentNode.Cell.Coordenates.y;

            var neighbourList = new List<PathNode>();

            if (coordenateX - 1 >= 0)
            {
                // Left
                neighbourList.Add(_grid.CreatePathNode(coordenateX - 1, coordenateY));
                // Left Down
                if (coordenateY - 1 >= 0) neighbourList.Add(_grid.CreatePathNode(coordenateX - 1, coordenateY - 1));
                // Left Up
                if (coordenateY + 1 < height) neighbourList.Add(_grid.CreatePathNode(coordenateX - 1, coordenateY + 1));
            }
            if (coordenateX + 1 < width)
            {
                // Right
                neighbourList.Add(_grid.CreatePathNode(coordenateX + 1, coordenateY));
                // Right Down
                if (coordenateY - 1 >= 0) neighbourList.Add(_grid.CreatePathNode(coordenateX + 1, coordenateY - 1));
                // Right Up
                if (coordenateY + 1 < height) neighbourList.Add(_grid.CreatePathNode(coordenateX + 1, coordenateY + 1));
            }
            // Down
            if (coordenateY - 1 >= 0) neighbourList.Add(_grid.CreatePathNode(coordenateX, coordenateY - 1));
            // Up
            if (coordenateY + 1 < height) neighbourList.Add(_grid.CreatePathNode(coordenateX, coordenateY + 1));

            return neighbourList;
        }

        /// <summary>
        /// Calculates the distance cost.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual int CalculateDistanceCost(PathNode a, PathNode b)
        {
            int xDistance = Mathf.Abs(a.Cell.Coordenates.x - b.Cell.Coordenates.x);
            int yDistance = Mathf.Abs(a.Cell.Coordenates.y - b.Cell.Coordenates.y);
            int remaining = Mathf.Abs(xDistance - yDistance);
            return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
        }

        /// <summary>
        /// Gets the lowest f cost node.
        /// </summary>
        /// <param name="pathNodeList">The path node list.</param>
        /// <returns></returns>
        protected virtual PathNode GetLowestFCostNode(Dictionary<GridCell, PathNode> pathNodeList)
        {
            var lowestFCostNode = pathNodeList.First().Value;
            foreach (var node in pathNodeList.Values.Where(x => x.TotalCost < lowestFCostNode.TotalCost))
            {
                lowestFCostNode = node;
            }

            return lowestFCostNode;
        }

        /// <summary>
        /// Sets the walkable area.
        /// </summary>
        protected void SetWalkableArea()
        {
            var size = new Vector2(1, 1);
            var cellCenter = 1 / 2;
            foreach (var item in _grid.GetAllCells())
            {
                var checknoWalkable = Physics2D.OverlapCircle(item.WorldPosition, cellCenter, colliderMask);
                if (checknoWalkable != null)
                {
                    item.IsWalkable = false;
                    continue;
                }
                item.IsWalkable = true;
            }
        }

        /// <summary>
        /// Builds the grid.
        /// </summary>
        protected abstract Grid BuildGrid();

        /// <summary>
        /// Gets the cell position.
        /// </summary>
        /// <param name="worldPosition">The world position.</param>
        /// <returns></returns>
        protected abstract Vector3Int GetCellPosition(Vector3 worldPosition);

#if UNITY_EDITOR

        /// <summary>
        /// Called when [draw gizmos].
        /// </summary>
        void OnDrawGizmos()
        {
            if (!showGizmos || _grid is null)
                return;

            var halfSize = _grid.CellSize / 2;
            var size3D = new Vector3(_grid.CellSize.x, _grid.CellSize.y, 0.01f);

            foreach (var cell in _grid.GetAllCells())
            {

                Gizmos.color = cell.IsWalkable ? Color.green : Color.red;
                Gizmos.DrawCube(cell.WorldPosition, size3D);


                Gizmos.DrawWireCube(cell.WorldPosition, _grid.CellSize);
            }
        }
#endif
    }
}
