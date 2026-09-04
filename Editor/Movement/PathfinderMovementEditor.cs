#if UNITY_EDITOR
using SparkyGames.Pathfinder;
using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder.Editor
{
    /// <summary>
    /// Inspector and Scene-view diagnostics for <see cref="PathfinderMovement"/>.
    /// Runtime values are read through the component's public snapshot properties.
    /// </summary>
    [CustomEditor(typeof(PathfinderMovement))]
    [CanEditMultipleObjects]
    public class PathfinderMovementEditor : UnityEditor.Editor
    {
        private SerializedProperty _speedProperty;
        private SerializedProperty _waypointToleranceProperty;
        private SerializedProperty _arrivalToleranceProperty;
        private SerializedProperty _requestPriorityProperty;
        private SerializedProperty _minimumRepathIntervalProperty;

        private static bool _showRuntime = true;
        private static bool _showPathList;
        private static bool _drawSceneGizmos = true;

        private void OnEnable()
        {
            _speedProperty = serializedObject.FindProperty("speed");
            _waypointToleranceProperty = serializedObject.FindProperty("waypointTolerance");
            _arrivalToleranceProperty = serializedObject.FindProperty("arrivalTolerance");
            _requestPriorityProperty = serializedObject.FindProperty("requestPriority");
            _minimumRepathIntervalProperty =
                serializedObject.FindProperty("minimumRepathInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Pathfinder Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_speedProperty);
            EditorGUILayout.PropertyField(_waypointToleranceProperty);
            EditorGUILayout.PropertyField(_arrivalToleranceProperty);
            EditorGUILayout.PropertyField(_requestPriorityProperty);
            EditorGUILayout.PropertyField(_minimumRepathIntervalProperty);

            EditorGUILayout.Space();
            _drawSceneGizmos = EditorGUILayout.ToggleLeft(
                "Draw gizmos in Scene view (selected)",
                _drawSceneGizmos);

            EditorGUILayout.Space();
            _showRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showRuntime,
                "Runtime (read-only)");
            if (_showRuntime && targets.Length == 1)
            {
                DrawRuntimeSection((PathfinderMovement)target);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();

            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private static void DrawRuntimeSection(PathfinderMovement movement)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("State", movement.State);
                EditorGUILayout.IntField("Operation ID", movement.OperationId);
                EditorGUILayout.Toggle("Pending movement", movement.HasPendingMovement);
                EditorGUILayout.EnumPopup("Request priority", movement.RequestPriority);
                var request = movement.PendingPathRequest;
                if (request != null)
                {
                    EditorGUILayout.LongField("Path request ID", request.RequestId);
                    EditorGUILayout.EnumPopup("Path request status", request.Status);
                }
                EditorGUILayout.EnumPopup("Last path status", movement.LastPathStatus);
                EditorGUILayout.IntField("Expanded nodes", movement.LastExpandedNodeCount);
                EditorGUILayout.IntField("Path cost", movement.LastPathCost);
                EditorGUILayout.LongField("Path grid version", movement.PathGridVersion);
                EditorGUILayout.IntField("Repath count", movement.RepathCount);
                EditorGUILayout.EnumPopup("Last replan reason", movement.LastRepathReason);
                EditorGUILayout.FloatField(
                    "Replan cooldown remaining",
                    movement.RemainingRepathCooldown);
                EditorGUILayout.Vector2Field("Current position", movement.CurrentPosition);
                EditorGUILayout.Vector3Field("Requested destination", movement.RequestedDestination);
                EditorGUILayout.Toggle("Has resolved destination", movement.HasResolvedDestination);
                if (movement.HasResolvedDestination)
                {
                    EditorGUILayout.Vector3Field("Resolved destination", movement.ResolvedDestination);
                }

                EditorGUILayout.Vector3Field("Next waypoint", movement.NextWaypoint);
                EditorGUILayout.Vector3Field("Movement direction", movement.MovementDirection);
                EditorGUILayout.Vector2Field("Actual velocity", movement.ActualVelocity);
                EditorGUILayout.FloatField("Actual speed", movement.ActualSpeed);
                EditorGUILayout.IntField("Current path index", movement.CurrentPathIndex);
                EditorGUILayout.IntField("Path count", movement.CurrentPath.Count);

                if (movement.CurrentPath.Count > 0)
                {
                    _showPathList = EditorGUILayout.Foldout(_showPathList, "Show path elements");
                    if (_showPathList)
                    {
                        EditorGUI.indentLevel++;
                        for (var i = 0; i < movement.CurrentPath.Count; i++)
                        {
                            EditorGUILayout.Vector3Field($"[{i}]", movement.CurrentPath[i]);
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           movement.State != PathfinderMovementState.FollowingPath))
                {
                    if (GUILayout.Button("Pause"))
                    {
                        movement.PauseMovement();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           movement.State != PathfinderMovementState.Paused))
                {
                    if (GUILayout.Button("Resume"))
                    {
                        movement.ResumeMovement();
                    }
                }

                using (new EditorGUI.DisabledScope(!movement.HasPendingMovement))
                {
                    if (GUILayout.Button("Cancel"))
                    {
                        movement.CancelMovement();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           movement.State != PathfinderMovementState.FollowingPath))
                {
                    if (GUILayout.Button("Replan"))
                    {
                        movement.RequestRepath();
                    }
                }
            }
        }

        public void OnSceneGUI()
        {
            if (!_drawSceneGizmos)
            {
                return;
            }

            var movement = (PathfinderMovement)target;
            var previousColor = Handles.color;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var current = movement.transform.position;
            Handles.color = Color.white;
            Handles.SphereHandleCap(
                0,
                current,
                Quaternion.identity,
                HandleUtility.GetHandleSize(current) * 0.1f,
                EventType.Repaint);
            Handles.Label(current + Vector3.up * 0.2f, "current");

            if (movement.State != PathfinderMovementState.Idle)
            {
                DrawDestination(movement.RequestedDestination, Color.yellow, "requested");
            }

            if (movement.HasResolvedDestination)
            {
                DrawDestination(movement.ResolvedDestination, Color.cyan, "resolved");
            }

            if (movement.CurrentPath.Count > 0)
            {
                var next = movement.NextWaypoint;
                Handles.color = Color.green;
                Handles.DrawWireDisc(
                    next,
                    Vector3.forward,
                    HandleUtility.GetHandleSize(next) * 0.15f);
                Handles.Label(next + Vector3.up * 0.2f, "next");
            }

            Handles.color = Color.cyan;
            var previous = current;
            for (var i = movement.CurrentPathIndex; i < movement.CurrentPath.Count; i++)
            {
                var waypoint = movement.CurrentPath[i];
                Handles.DrawLine(previous, waypoint);
                Handles.DrawSolidDisc(
                    waypoint,
                    Vector3.forward,
                    HandleUtility.GetHandleSize(waypoint) * 0.05f);
                previous = waypoint;
            }

            Handles.color = previousColor;
        }

        private static void DrawDestination(Vector3 position, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawWireDisc(
                position,
                Vector3.forward,
                HandleUtility.GetHandleSize(position) * 0.2f);
            Handles.Label(position + Vector3.up * 0.2f, label);
        }
    }
}
#endif
