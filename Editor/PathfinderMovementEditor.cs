#if UNITY_EDITOR
using SparkyGames.Pathfinder;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SparkyGames.Pathfinder.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="PathfinderMovement"/> that exposes serialized fields
    /// and shows key runtime variables in a read-only section. Also draws helpful gizmos
    /// in the Scene view when the GameObject is selected.
    /// </summary>
    [CustomEditor(typeof(PathfinderMovement))]
    [CanEditMultipleObjects]
    public class PathfinderMovementEditor : UnityEditor.Editor
    {
        private SerializedProperty _speedProp;
        private SerializedProperty _isActiveProp;

        // Scene gizmo toggles
        private static bool _showRuntime = true;
        private static bool _showPathList = false;
        private static bool _drawSceneGizmos = true;

        private void OnEnable()
        {
            _speedProp = serializedObject.FindProperty("speed");
            _isActiveProp = serializedObject.FindProperty("isActive");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Pathfinder Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_speedProp);
            EditorGUILayout.PropertyField(_isActiveProp);

            EditorGUILayout.Space();
            _drawSceneGizmos = EditorGUILayout.ToggleLeft("Draw gizmos in Scene view (selected)", _drawSceneGizmos);

            EditorGUILayout.Space();
            _showRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_showRuntime, "Runtime (read-only)");
            if (_showRuntime)
            {
                DrawRuntimeSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            // Auto-refresh while playing so values update live
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawRuntimeSection()
        {
            var comp = (PathfinderMovement)target;

            using (new EditorGUI.DisabledScope(true))
            {
                // Property (public getter)
                EditorGUILayout.Vector3Field("MovementDirection (prop)", comp.MovementDirection);

                // Private fields via reflection
                var transformPos = GetPrivateField<Vector2>(comp, "_transformPosition");
                var targetPos = GetPrivateField<Vector2>(comp, "_targetPosition");
                var targetNextPos = GetPrivateField<Vector2>(comp, "_targetNextPosition");
                var currentIndex = GetPrivateField<int>(comp, "_currentPathIndex");
                var movementDir = GetPrivateField<Vector3>(comp, "_movementDirection");
                var pathList = GetPrivateField<List<Vector3>>(comp, "_pathVectorList");

                EditorGUILayout.Vector2Field("Transform Position", transformPos);
                EditorGUILayout.Vector2Field("Target Position", targetPos);
                EditorGUILayout.Vector2Field("Next Waypoint", targetNextPos);
                EditorGUILayout.Vector3Field("Movement Direction (field)", movementDir);
                EditorGUILayout.IntField("Current Path Index", currentIndex);

                int count = pathList != null ? pathList.Count : 0;
                EditorGUILayout.IntField("Path Count", count);

                // Optional per-element view
                if (count > 0)
                {
                    _showPathList = EditorGUILayout.Foldout(_showPathList, "Show Path Elements");
                    if (_showPathList)
                    {
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < count; i++)
                        {
                            EditorGUILayout.Vector3Field($"[{i}]", pathList[i]);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Activate"))
                {
                    SetPrivateField(comp, "isActive", true, isSerializedField: true);
                }
                if (GUILayout.Button("Deactivate"))
                {
                    SetPrivateField(comp, "isActive", false, isSerializedField: true);
                }
            }
        }

        public void OnSceneGUI()
        {
            if (!_drawSceneGizmos) return;

            var comp = (PathfinderMovement)target;
            var pathList = GetPrivateField<List<Vector3>>(comp, "_pathVectorList");
            var nextPos = GetPrivateField<Vector2>(comp, "_targetNextPosition");
            var targetPos = GetPrivateField<Vector2>(comp, "_targetPosition");
            var tr = comp.transform;

            // Draw current position
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.SphereHandleCap(0, tr.position, Quaternion.identity, HandleUtility.GetHandleSize(tr.position) * 0.1f, EventType.Repaint);
            Handles.Label(tr.position + Vector3.up * 0.2f, "current");

            // Draw next waypoint
            var next = (Vector3)nextPos;
            Handles.DrawWireDisc(next, Vector3.forward, HandleUtility.GetHandleSize(next) * 0.15f);
            Handles.Label(next + Vector3.up * 0.2f, "next");

            // Draw final target
            var tgt = (Vector3)targetPos;
            Handles.DrawWireDisc(tgt, Vector3.forward, HandleUtility.GetHandleSize(tgt) * 0.2f);
            Handles.Label(tgt + Vector3.up * 0.2f, "target");

            // Draw path polyline
            if (pathList != null && pathList.Count > 0)
            {
                Handles.DrawAAPolyLine(2f, pathList.ToArray());
                foreach (var p in pathList)
                {
                    Handles.DrawSolidDisc(p, Vector3.forward, HandleUtility.GetHandleSize(p) * 0.05f);
                }
            }
        }

        // --- Reflection helpers ---

        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            if (obj == null) return default;
            var type = obj.GetType();
            var fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi == null) return default;
            object val = fi.GetValue(obj);
            if (val == null) return default;
            return (T)val;
        }

        private static void SetPrivateField(object obj, string fieldName, object value, bool isSerializedField = false)
        {
            if (obj == null) return;
            var type = obj.GetType();
            var fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi == null)
            {
                // Try property instead
                var pi = type.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (pi != null && pi.CanWrite) pi.SetValue(obj, value);
                return;
            }

            fi.SetValue(obj, value);
            if (isSerializedField)
            {
                EditorUtility.SetDirty((Object)obj);
            }
        }
    }
}
#endif
