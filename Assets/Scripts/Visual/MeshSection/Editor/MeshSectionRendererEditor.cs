using UnityEditor;
using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection.Editor
{
    [CustomEditor(typeof(MeshSectionRenderer))]
    [CanEditMultipleObjects]
    public sealed class MeshSectionRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty _warnOnInvalidSetupProperty;

        private void OnEnable()
        {
            _warnOnInvalidSetupProperty = serializedObject.FindProperty("_warnOnInvalidSetup");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawValidationBanner();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_warnOnInvalidSetupProperty);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "MeshSectionRenderer validates that the attached Renderer uses the " +
                "MeshSectionLit shader. Cap fill is handled by the MeshSectionCapFeature " +
                "in your URP Renderer, driven by each MeshSectionArea's CapProfile.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawValidationBanner()
        {
            bool anyMissingLit = false;

            foreach (Object targetObject in targets)
            {
                if (targetObject is not MeshSectionRenderer component)
                {
                    continue;
                }

                if (!component.HasLitMaterial())
                {
                    anyMissingLit = true;
                    break;
                }
            }

            if (anyMissingLit)
            {
                EditorGUILayout.HelpBox(
                    $"Missing a material using '{MeshSectionRenderer.LitShaderName}'. " +
                    "Cutting will not work until you add one.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Material setup is valid.", MessageType.Info);
            }
        }

        [MenuItem("CONTEXT/Renderer/Add Mesh Section Renderer")]
        private static void AddComponentToRenderer(MenuCommand command)
        {
            if (command.context is not Renderer renderer)
            {
                return;
            }

            if (renderer.gameObject.GetComponent<MeshSectionRenderer>() != null)
            {
                return;
            }

            Undo.AddComponent<MeshSectionRenderer>(renderer.gameObject);
        }
    }
}
