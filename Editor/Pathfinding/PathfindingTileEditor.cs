using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder.Editor
{
    /// <summary>
    /// Pathfinding Tilemap Editor
    /// </summary>
    /// <seealso cref="UnityEditor.Editor" />
    [CustomEditor(typeof(PathfindingTilemap))]
    public class PathfindingTileEditor : UnityEditor.Editor
    {
        private PathfindingTilemap _pathfinding;

        /// <summary>
        /// Called when [enable].
        /// </summary>
        private void OnEnable()
        {
            _pathfinding = (PathfindingTilemap)target;
        }

        /// <summary>
        /// Implement this function to make a custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawNavigationSemantics();
            var configurationValid =
                PathfindingInspectorDiagnostics.DrawConfigurationStatus(
                    _pathfinding);
            PathfindingInspectorDiagnostics.DrawRefreshControls(
                _pathfinding,
                configurationValid);
            DrawNavigationSnapshot();
            DrawGizmoSummary();
        }

        private void DrawNavigationSemantics()
        {
            EditorGUILayout.Space();
            var options = _pathfinding.GridSourceOptions;
            var semanticsMessage = options.CellSemantics ==
                TilemapCellSemantics.TilesDefineNavigableArea
                ? "Cada tile representa terreno potencialmente navegable. Los huecos quedan bloqueados."
                : "Modo de bounds: toda la región representa terreno, incluso las posiciones sin tile.";
            EditorGUILayout.HelpBox(semanticsMessage, MessageType.Info);
        }

        private void DrawNavigationSnapshot()
        {
            if (!_pathfinding.HasGrid)
            {
                return;
            }

            if (_pathfinding.GridSource is not TilemapGridSource source)
            {
                return;
            }

            EditorGUILayout.LabelField("Navigation snapshot", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Grid version", _pathfinding.GridVersion.ToString());
            EditorGUILayout.LabelField("Effective bounds", source.EffectiveBounds.ToString());
            EditorGUILayout.LabelField("Candidate cells", source.CandidateCellCount.ToString());
            EditorGUILayout.LabelField("Occupied tiles", source.OccupiedTileCount.ToString());
            EditorGUILayout.LabelField("Walkable cells", source.WalkableCellCount.ToString());
            EditorGUILayout.LabelField("Tile rules", source.TileRuleCount.ToString());
            EditorGUILayout.LabelField(
                "Static Physics2D sampling",
                source.SamplesStaticObstacles.ToString());
            EditorGUILayout.LabelField(
                "Weighted terrain",
                _pathfinding.HasWeightedTerrain.ToString());

            var lastUpdate = _pathfinding.LastGridRegionUpdate;
            if (lastUpdate.CurrentVersion > 0)
            {
                EditorGUILayout.LabelField(
                    "Last region cells",
                    $"{lastUpdate.ChangedCellCount}/{lastUpdate.EvaluatedCellCount} changed");
                EditorGUILayout.LabelField(
                    "Walkability / cost",
                    $"{lastUpdate.ChangedWalkabilityCellCount} / " +
                    lastUpdate.ChangedTraversalCostCellCount);
            }

        }

        private void DrawGizmoSummary()
        {
            if (!_pathfinding.ShowGizmos)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected gizmo cost", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Detail", _pathfinding.GizmoDetail.ToString());
            EditorGUILayout.LabelField(
                "Cells inspected / total",
                $"{_pathfinding.GizmoInspectedCellCount} / " +
                _pathfinding.GridCellCount);
            if (_pathfinding.GizmoDetail ==
                    PathfindingGizmoDetail.SampledCells &&
                _pathfinding.GizmoInspectedCellCount <
                _pathfinding.GridCellCount)
            {
                EditorGUILayout.HelpBox(
                    "Large grids use a uniform bounded sample. Filters apply to " +
                    "that sample and are not an exhaustive cell query.",
                    MessageType.Info);
            }
        }
    }
}
