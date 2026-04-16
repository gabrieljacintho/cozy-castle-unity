using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("GabrielBertasso/MeshSection/Mesh Section Area")]
    public sealed class MeshSectionArea : MonoBehaviour
    {
        [SerializeField, Tooltip("Center offset in local space. Final center = transform.position + rotated(localCenter).")]
        private Vector3 _localCenter = Vector3.zero;

        [SerializeField, Tooltip("Full size of the cutting box along each axis.")]
        private Vector3 _size = Vector3.one;

        [SerializeField, Range(0f, 1f), Tooltip("0 = fully hidden inside the box. 1 = fully visible (area is effectively off).")]
        private float _alpha = 0f;

        [SerializeField, Min(0.001f), Tooltip("World-space thickness of the intersection band drawn with the intersection material.")]
        private float _edgeThickness = 0.05f;

        [SerializeField, Min(0.0001f), Tooltip("World-space softness of the hide boundary. Keep small for crisp cuts.")]
        private float _featherDistance = 0.001f;

        [SerializeField, Tooltip("Intersection material drawn where meshes are cut by this area. If null, cap is not rendered for this area.")]
        private MeshSectionCapProfile _capProfile;

        [SerializeField, Tooltip("Color used to draw the gizmo in the scene view.")]
        private Color _gizmoColor = new Color(1f, 0.35f, 0.1f, 0.6f);

        private Vector3 _lastCenter;
        private float _lastAlpha;
        private bool _hasBaseline;
        private bool _changedFlag = true;

        public float Alpha
        {
            get => _alpha;
            set
            {
                float clamped = Mathf.Clamp01(value);

                if (!Mathf.Approximately(clamped, _alpha))
                {
                    _alpha = clamped;
                    _changedFlag = true;
                    MeshSectionAreaManager.MarkDirty();
                }
            }
        }

        public Vector3 Size
        {
            get => _size;
            set
            {
                Vector3 sanitized = new Vector3(
                    Mathf.Max(0f, value.x),
                    Mathf.Max(0f, value.y),
                    Mathf.Max(0f, value.z));

                if (sanitized != _size)
                {
                    _size = sanitized;
                    _changedFlag = true;
                    MeshSectionAreaManager.MarkDirty();
                }
            }
        }

        public float EdgeThickness
        {
            get => _edgeThickness;
            set
            {
                float sanitized = Mathf.Max(0.001f, value);

                if (!Mathf.Approximately(sanitized, _edgeThickness))
                {
                    _edgeThickness = sanitized;
                    _changedFlag = true;
                    MeshSectionAreaManager.MarkDirty();
                }
            }
        }

        public MeshSectionCapProfile CapProfile => _capProfile;

        private void OnEnable()
        {
            _hasBaseline = false;
            _changedFlag = true;
            MeshSectionAreaManager.Register(this);
        }

        private void OnDisable()
        {
            MeshSectionAreaManager.Unregister(this);
        }

        private void OnValidate()
        {
            _size.x = Mathf.Max(0f, _size.x);
            _size.y = Mathf.Max(0f, _size.y);
            _size.z = Mathf.Max(0f, _size.z);
            _edgeThickness = Mathf.Max(0.001f, _edgeThickness);
            _featherDistance = Mathf.Max(0.0001f, _featherDistance);
            _alpha = Mathf.Clamp01(_alpha);
            _changedFlag = true;
            MeshSectionAreaManager.MarkDirty();
        }

        private void OnDrawGizmos()
        {
            DrawGizmo(filled: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(filled: true);
        }

        public bool HasChanged()
        {
            if (!_hasBaseline)
            {
                return true;
            }

            if (_changedFlag)
            {
                return true;
            }

            Transform t = transform;

            if (t.hasChanged)
            {
                return true;
            }

            Vector3 currentCenter = t.TransformPoint(_localCenter);

            if ((currentCenter - _lastCenter).sqrMagnitude > 1e-10f)
            {
                return true;
            }

            return false;
        }

        public void ClearChangedFlag()
        {
            _changedFlag = false;
            transform.hasChanged = false;
        }

        public void FillShaderData(
            out Vector3 center,
            out Vector3 halfExtents,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward,
            out float alpha,
            out float edgeThickness,
            out float feather)
        {
            Transform t = transform;
            Vector3 lossyScale = t.lossyScale;

            center = t.TransformPoint(_localCenter);
            halfExtents = new Vector3(
                Mathf.Max(0f, _size.x * Mathf.Abs(lossyScale.x) * 0.5f),
                Mathf.Max(0f, _size.y * Mathf.Abs(lossyScale.y) * 0.5f),
                Mathf.Max(0f, _size.z * Mathf.Abs(lossyScale.z) * 0.5f));

            right = t.right;
            up = t.up;
            forward = t.forward;

            alpha = _alpha;
            edgeThickness = _edgeThickness;
            feather = _featherDistance;

            _lastCenter = center;
            _lastAlpha = alpha;
            _hasBaseline = true;
        }

        // World-space TRS matrix describing the cutting box, for use when drawing
        // the cap cube. Unity's built-in cube primitive is 1x1x1 centered at origin,
        // so the matrix directly scales it to the area's size.
        public Matrix4x4 GetBoxMatrix()
        {
            Transform t = transform;
            Vector3 lossyScale = t.lossyScale;
            Vector3 worldCenter = t.TransformPoint(_localCenter);
            Vector3 worldSize = new Vector3(
                _size.x * Mathf.Abs(lossyScale.x),
                _size.y * Mathf.Abs(lossyScale.y),
                _size.z * Mathf.Abs(lossyScale.z));

            return Matrix4x4.TRS(worldCenter, t.rotation, worldSize);
        }

        private void DrawGizmo(bool filled)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Transform t = transform;
            Gizmos.matrix = Matrix4x4.TRS(t.TransformPoint(_localCenter), t.rotation, t.lossyScale);

            Color outline = _gizmoColor;
            outline.a = 1f;
            Gizmos.color = outline;
            Gizmos.DrawWireCube(Vector3.zero, _size);

            if (filled)
            {
                Color fill = _gizmoColor;
                fill.a *= 0.15f;
                Gizmos.color = fill;
                Gizmos.DrawCube(Vector3.zero, _size);
            }

            Gizmos.matrix = previous;
        }
    }
}
