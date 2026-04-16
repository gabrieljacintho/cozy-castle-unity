using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("GabrielBertasso/MeshSection/Mesh Section Renderer")]
    public sealed class MeshSectionRenderer : MonoBehaviour
    {
        public const string LitShaderName = "GabrielBertasso/MeshSection/MeshSectionLit";

        [SerializeField, Tooltip("Log warnings when the renderer doesn't have a MeshSectionLit material.")]
        private bool _warnOnInvalidSetup = true;

        private Renderer _renderer;

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnEnable()
        {
            CacheRenderer();

            if (_warnOnInvalidSetup)
            {
                ValidateSetup();
            }
        }

        public bool HasLitMaterial()
        {
            CacheRenderer();

            if (_renderer == null)
            {
                return false;
            }

            Material[] materials = _renderer.sharedMaterials;
            if (materials == null)
            {
                return false;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (material.shader.name == LitShaderName)
                {
                    return true;
                }
            }

            return false;
        }

        public void ValidateSetup()
        {
            CacheRenderer();

            if (_renderer == null)
            {
                return;
            }

            if (!HasLitMaterial())
            {
                Debug.LogWarning(
                    $"[MeshSection] Renderer '{name}' is missing a material using '{LitShaderName}'. " +
                    "Cutting will not work on this mesh.",
                    this);
            }
        }

        private void CacheRenderer()
        {
            if (_renderer == null)
            {
                TryGetComponent(out _renderer);
            }
        }
    }
}
