using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder.Editor
{
    #if UNITY_EDITOR
    /// <summary>
    /// Pathfinding Rectangle Editor
    /// </summary>
    /// <seealso cref="UnityEditor.Editor" />
    [CustomEditor(typeof(PathfindingRectangle))]
    public class PathfindingRectangleEditor : UnityEditor.Editor
    {
        private PathfindingRectangle _pathfinding;

        /// <summary>
        /// Called when [enable].
        /// </summary>
        private void OnEnable()
        {
            _pathfinding = (PathfindingRectangle)target;
        }

        /// <summary>
        /// Implement this function to make a custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var configurationValid =
                PathfindingInspectorDiagnostics.DrawConfigurationStatus(
                    _pathfinding);
            PathfindingInspectorDiagnostics.DrawRefreshControls(
                _pathfinding,
                configurationValid);
            DrawGridSummary();
            DrawGizmoSummary();
        }

        private void DrawGridSummary()
        {
            if (_pathfinding.HasGrid)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Navigation snapshot",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Grid version",
                    _pathfinding.GridVersion.ToString());
                EditorGUILayout.LabelField(
                    "Cells / configured maximum",
                    $"{_pathfinding.GridCellCount} / " +
                    _pathfinding.MaximumGridCells);
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

    internal static class PathfindingInspectorDiagnostics
    {
        public static bool DrawConfigurationStatus(Pathfinding pathfinding)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Navigation configuration",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Only Static Obstacle Mask is baked into the grid. Dynamic Obstacle " +
                "Mask and Agent Mask remain runtime-only, and the three masks cannot " +
                "share layers.",
                MessageType.Info);

            var configurationValid =
                pathfinding.TryValidateConfiguration(out var errorMessage);
            if (!configurationValid)
            {
                EditorGUILayout.HelpBox(
                    "Navigation configuration is invalid. Grid construction is " +
                    "disabled until this is fixed.\n\n" + errorMessage,
                    MessageType.Error);
            }
            else if (!string.IsNullOrWhiteSpace(pathfinding.LastGridBuildError))
            {
                EditorGUILayout.HelpBox(
                    "Configuration preflight passed, but the most recent grid build " +
                    "failed:\n\n" + pathfinding.LastGridBuildError +
                    "\n\nFix the scene content if necessary, then rebuild the grid.",
                    MessageType.Error);
            }
            else if (!pathfinding.HasGrid)
            {
                EditorGUILayout.HelpBox(
                    "Configuration preflight passed. No grid snapshot is available; " +
                    "rebuild the preview or enter Play Mode.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Configuration preflight passed. Grid version " +
                    $"{pathfinding.GridVersion} is active.",
                    MessageType.None);
            }

            if (!string.IsNullOrWhiteSpace(pathfinding.LastGridUpdateError))
            {
                EditorGUILayout.HelpBox(
                    "The most recent regional update failed:\n\n" +
                    pathfinding.LastGridUpdateError,
                    MessageType.Error);
            }

            return configurationValid;
        }

        public static void DrawRefreshControls(
            Pathfinding pathfinding,
            bool configurationValid)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "The preview is not rebuilt from OnValidate or Transform changes. " +
                "Awake builds the grid in Play Mode; use this button for an explicit " +
                "Edit Mode refresh.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!configurationValid))
            {
                if (!GUILayout.Button(
                        Application.isPlaying
                            ? "Rebuild navigation grid"
                            : "Rebuild grid preview"))
                {
                    return;
                }

                pathfinding.Refresh();
                SceneView.RepaintAll();
            }
        }
    }
    #endif
}
