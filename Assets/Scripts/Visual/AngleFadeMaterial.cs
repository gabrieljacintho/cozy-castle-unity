using UnityEngine;
using UnityEngine.Rendering;

namespace GabrielBertasso.Visual
{
    public class AngleFadeMaterial : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;

        [Header("Angle Settings")]
        [SerializeField, Range(1f, 180f), Tooltip("Horizontal half-angle in degrees. The camera must be within this angle on the horizontal plane relative to the object's forward to trigger the fade-in.")]
        private float _halfAngle = 45f;

        [Header("Fade Settings")]
        [SerializeField, Range(0.01f, 50f), Tooltip("Speed at which the alpha increases when the camera is inside the angle.")]
        private float _fadeInSpeed = 5f;

        [SerializeField, Range(0.01f, 50f), Tooltip("Speed at which the alpha decreases when the camera is outside the angle.")]
        private float _fadeOutSpeed = 5f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum alpha value when fully faded out.")]
        private float _minAlpha = 0f;

        [Header("References")]
        [SerializeField, Tooltip("The camera transform used for angle checking. If null, Camera.main will be used.")]
        private Transform _cameraTransform;

        private Material _material;
        private float _currentAlpha;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceTypeId = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

        private void Awake()
        {
            var renderer = _renderer ? _renderer : GetComponent<Renderer>();
            _material = renderer.material;

            SetupTransparentMaterial();

            _currentAlpha = _material.GetColor(BaseColorId).a;
            ApplyAlpha(_currentAlpha);
        }

        private void Start()
        {
            if (_cameraTransform == null)
            {
                UnityEngine.Camera mainCamera = UnityEngine.Camera.main;

                if (mainCamera != null)
                {
                    _cameraTransform = mainCamera.transform;
                }
                else
                {
                    Debug.LogWarning($"[{nameof(AngleFadeMaterial)}] No camera assigned and Camera.main is null.", this);
                    enabled = false;
                }
            }
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            bool isInsideAngle = IsCameraInsideAngle();
            float targetAlpha = isInsideAngle ? _minAlpha : 1f;
            float speed = isInsideAngle ? _fadeInSpeed : _fadeOutSpeed;

            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, speed * Time.deltaTime);
            ApplyAlpha(_currentAlpha);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private bool IsCameraInsideAngle()
        {
            Vector3 directionToCamera = _cameraTransform.position - transform.position;
            directionToCamera = Vector3.ProjectOnPlane(directionToCamera, transform.up).normalized;
            float angle = Vector3.Angle(transform.forward, directionToCamera);

            return angle <= _halfAngle;
        }

        private void ApplyAlpha(float alpha)
        {
            Color color = _material.GetColor(BaseColorId);
            color.a = alpha;
            _material.SetColor(BaseColorId, color);
        }

        private void SetupTransparentMaterial()
        {
            _material.SetFloat(SurfaceTypeId, 1f);
            _material.SetFloat(BlendModeId, 0f);
            _material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            _material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat(ZWriteId, 0f);
            _material.SetFloat(AlphaClipId, 0f);

            _material.SetOverrideTag("RenderType", "Transparent");
            _material.renderQueue = (int)RenderQueue.Transparent;

            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.DisableKeyword("_ALPHATEST_ON");
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 position = transform.position;
            Vector3 forward = transform.forward;
            float gizmoLength = 2f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(position, forward * gizmoLength);

            Quaternion leftRotation = Quaternion.AngleAxis(-_halfAngle, transform.up);
            Quaternion rightRotation = Quaternion.AngleAxis(_halfAngle, transform.up);

            Vector3 leftDirection = leftRotation * forward;
            Vector3 rightDirection = rightRotation * forward;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
            Gizmos.DrawRay(position, leftDirection * gizmoLength);
            Gizmos.DrawRay(position, rightDirection * gizmoLength);

            DrawWireArc(position, transform.up, leftDirection, _halfAngle * 2f, gizmoLength, 20);

            if (_cameraTransform != null)
            {
                bool isInside = IsCameraInsideAngle();
                Gizmos.color = isInside ? Color.green : Color.red;
                Gizmos.DrawLine(position, _cameraTransform.position);
            }
        }

        private static void DrawWireArc(Vector3 center, Vector3 axis, Vector3 from, float totalAngle, float radius, int segments)
        {
            float stepAngle = totalAngle / segments;
            Vector3 previousPoint = center + from.normalized * radius;

            for (int i = 1; i <= segments; i++)
            {
                Quaternion rotation = Quaternion.AngleAxis(stepAngle * i, axis);
                Vector3 nextPoint = center + rotation * from.normalized * radius;
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}