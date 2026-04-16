using UnityEditor;
using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection.Editor
{
    [CustomEditor(typeof(MeshSectionArea))]
    [CanEditMultipleObjects]
    public sealed class MeshSectionAreaEditor : UnityEditor.Editor
    {
        private SerializedProperty _localCenterProperty;
        private SerializedProperty _sizeProperty;
        private SerializedProperty _alphaProperty;
        private SerializedProperty _edgeThicknessProperty;
        private SerializedProperty _featherDistanceProperty;
        private SerializedProperty _capProfileProperty;
        private SerializedProperty _gizmoColorProperty;

        private void OnEnable()
        {
            _localCenterProperty = serializedObject.FindProperty("_localCenter");
            _sizeProperty = serializedObject.FindProperty("_size");
            _alphaProperty = serializedObject.FindProperty("_alpha");
            _edgeThicknessProperty = serializedObject.FindProperty("_edgeThickness");
            _featherDistanceProperty = serializedObject.FindProperty("_featherDistance");
            _capProfileProperty = serializedObject.FindProperty("_capProfile");
            _gizmoColorProperty = serializedObject.FindProperty("_gizmoColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_localCenterProperty);
            EditorGUILayout.PropertyField(_sizeProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cutting", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_alphaProperty);
            EditorGUILayout.PropertyField(_edgeThicknessProperty);
            EditorGUILayout.PropertyField(_featherDistanceProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cap Fill", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_capProfileProperty);

            if (_capProfileProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No Cap Profile assigned. Cuts from this area will show whatever is behind the cut (skybox, other geometry) instead of a solid fill.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gizmo", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gizmoColorProperty);

            if (MeshSectionAreaManager.Instance != null)
            {
                int activeCount = MeshSectionAreaManager.Instance.ActiveAreaCount;
                int max = MeshSectionAreaManager.MaxAreas;

                if (activeCount >= max)
                {
                    EditorGUILayout.HelpBox(
                        $"{activeCount}/{max} cutting areas active. Adding more will be ignored at runtime.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"{activeCount}/{max} cutting areas active.", MessageType.Info);
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fit to Children Bounds"))
                {
                    FitToChildrenBounds();
                }

                if (GUILayout.Button("Reset Size"))
                {
                    ResetSize();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void FitToChildrenBounds()
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is not MeshSectionArea area)
                {
                    continue;
                }

                Renderer[] childRenderers = area.GetComponentsInChildren<Renderer>();

                if (childRenderers == null || childRenderers.Length == 0)
                {
                    continue;
                }

                Bounds bounds = childRenderers[0].bounds;

                for (int i = 1; i < childRenderers.Length; i++)
                {
                    bounds.Encapsulate(childRenderers[i].bounds);
                }

                Transform t = area.transform;
                Vector3 localCenter = t.InverseTransformPoint(bounds.center);
                Vector3 lossyScale = t.lossyScale;
                Vector3 size = new Vector3(
                    bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x)),
                    bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y)),
                    bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z)));

                Undo.RecordObject(area, "Fit MeshSectionArea To Children");
                var so = new SerializedObject(area);
                so.FindProperty("_localCenter").vector3Value = localCenter;
                so.FindProperty("_size").vector3Value = size;
                so.ApplyModifiedProperties();
            }
        }

        private void ResetSize()
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is not MeshSectionArea area)
                {
                    continue;
                }

                Undo.RecordObject(area, "Reset MeshSectionArea Size");
                var so = new SerializedObject(area);
                so.FindProperty("_size").vector3Value = Vector3.one;
                so.FindProperty("_localCenter").vector3Value = Vector3.zero;
                so.ApplyModifiedProperties();
            }
        }

        [MenuItem("GameObject/GabrielBertasso/MeshSection/Create Area", false, 10)]
        private static void CreateAreaMenuItem(MenuCommand menuCommand)
        {
            var go = new GameObject("MeshSectionArea");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            go.AddComponent<MeshSectionArea>();
            Undo.RegisterCreatedObjectUndo(go, "Create MeshSectionArea");
            Selection.activeObject = go;
        }
    }
}
