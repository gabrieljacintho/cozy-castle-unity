using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection
{
    [CreateAssetMenu(
        fileName = "MeshSectionCapProfile",
        menuName = "GabrielBertasso/MeshSection/Cap Profile",
        order = 0)]
    public sealed class MeshSectionCapProfile : ScriptableObject
    {
        [Header("Albedo")]
        [SerializeField, Tooltip("Base color texture sampled across the cap surface. If null, uses the color only.")]
        private Texture2D _albedoMap;

        [SerializeField, ColorUsage(showAlpha: false)]
        private Color _albedoColor = Color.white;

        [Header("Normal")]
        [SerializeField, Tooltip("Normal map sampled across the cap surface. If null, cap uses the box face normal directly.")]
        private Texture2D _normalMap;

        [SerializeField, Min(0f)]
        private float _normalScale = 1f;

        [Header("Surface")]
        [SerializeField, Range(0f, 1f)]
        private float _metallic = 0f;

        [SerializeField, Range(0f, 1f)]
        private float _smoothness = 0.2f;

        [Header("Emission")]
        [SerializeField, ColorUsage(showAlpha: false, hdr: true)]
        private Color _emissionColor = Color.black;

        [Header("Mapping")]
        [SerializeField, Min(0.0001f), Tooltip("World-space size of one texture tile. 1.0 = one tile per meter.")]
        private float _textureScale = 1f;

        public Texture2D AlbedoMap => _albedoMap;
        public Color AlbedoColor => _albedoColor;
        public Texture2D NormalMap => _normalMap;
        public float NormalScale => _normalScale;
        public float Metallic => _metallic;
        public float Smoothness => _smoothness;
        public Color EmissionColor => _emissionColor;
        public float TextureScale => _textureScale;
    }
}
