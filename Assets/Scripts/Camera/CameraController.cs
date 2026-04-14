using UnityEngine;
using Lean.Touch;

namespace GabrielBertasso.Camera
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The transform the camera will orbit around.")]
        private Transform _target;

        [SerializeField, Tooltip("The camera transform to control.")]
        private Transform _cameraTransform;

        [Header("Horizontal Orbit")]
        [SerializeField, Tooltip("Horizontal rotation speed in degrees per pixel.")]
        private float _horizontalSpeed = 0.5f;

        [SerializeField, Tooltip("Smoothing factor for horizontal rotation. Lower values mean smoother.")]
        [Range(0.01f, 1f)]
        private float _horizontalSmoothing = 0.15f;

        [Header("Zoom (Pinch)")]
        [SerializeField, Tooltip("Minimum orbit radius.")]
        private float _minOrbitRadius = 2.5f;

        [SerializeField, Tooltip("Maximum orbit radius.")]
        private float _maxOrbitRadius = 12f;

        [SerializeField, Tooltip("Smoothing factor for zoom.")]
        [Range(0.01f, 1f)]
        private float _zoomSmoothing = 0.15f;

        [SerializeField, Tooltip("How far the orbit radius can stretch beyond limits before snapping back.")]
        private float _zoomElasticOvershoot = 1.5f;

        [SerializeField, Tooltip("Speed at which the orbit radius snaps back from the elastic overshoot.")]
        private float _zoomElasticReturnSpeed = 10f;

        [Header("Vertical Movement")]
        [SerializeField, Tooltip("Vertical movement speed in units per pixel.")]
        private float _verticalSpeed = 0.02f;

        [SerializeField, Tooltip("Smoothing factor for vertical movement.")]
        [Range(0.01f, 1f)]
        private float _verticalSmoothing = 0.15f;

        [SerializeField, Tooltip("Minimum vertical offset from the target (world units).")]
        private float _minHeight = 1f;

        [SerializeField, Tooltip("Maximum vertical offset from the target (world units).")]
        private float _maxHeight = 20f;

        [SerializeField, Tooltip("How far the camera can stretch beyond the limits before snapping back.")]
        private float _elasticOvershoot = 2f;

        [SerializeField, Tooltip("Speed at which the camera snaps back from the elastic overshoot.")]
        private float _elasticReturnSpeed = 8f;

        [Header("Isometric View")]
        [SerializeField, Tooltip("Constant pitch (down tilt) in degrees for isometric view.")]
        [Range(0f, 89f)]
        private float _isometricPitch = 35f;

        [SerializeField, Tooltip("Vertical offset applied to the orbit pivot (target position) in world units.")]
        private float _pivotHeightOffset;

        [Header("Input")]
        [SerializeField] private bool _ignoreGui = true;

        private float _currentAngle;
        private float _targetAngle;
        private float _currentHeight;
        private float _targetHeight;
        private float _currentOrbitRadius;
        private float _targetOrbitRadius;
        private bool _isDragging;
        private bool _isPinching;

        private void Awake()
        {
            Vector3 offset = _cameraTransform.position - GetPivotPosition();

            _currentAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _targetAngle = _currentAngle;

            float horizontalDist = new Vector2(offset.x, offset.z).magnitude;
            _currentOrbitRadius = horizontalDist / Mathf.Cos(_isometricPitch * Mathf.Deg2Rad);
            _targetOrbitRadius = _currentOrbitRadius;

            Vector3 orbitOffset = GetOrbitOffset(_currentAngle, _currentOrbitRadius);
            _currentHeight = offset.y - orbitOffset.y;
            _targetHeight = _currentHeight;
        }

        private void OnEnable()
        {
            LeanTouch.OnFingerUpdate += HandleFingerUpdate;
            LeanTouch.OnFingerUp += HandleFingerUp;
            LeanTouch.OnGesture += HandleGesture;
        }

        private void OnDisable()
        {
            LeanTouch.OnFingerUpdate -= HandleFingerUpdate;
            LeanTouch.OnFingerUp -= HandleFingerUp;
            LeanTouch.OnGesture -= HandleGesture;
        }

        private void LateUpdate()
        {
            ApplyElasticBounds();
            SmoothValues();
            ApplyCamera();
        }

        private void HandleFingerUpdate(LeanFinger finger)
        {
            if (_ignoreGui && finger.IsOverGui) return;
            if (_isPinching) return;

            _isDragging = true;
            Vector2 delta = finger.ScaledDelta;
            _targetAngle += delta.x * _horizontalSpeed;
            _targetHeight -= delta.y * _verticalSpeed;
            _targetHeight = Mathf.Clamp(
                _targetHeight,
                _minHeight - _elasticOvershoot,
                _maxHeight + _elasticOvershoot
            );
        }

        private void HandleFingerUp(LeanFinger finger)
        {
            _isDragging = false;
        }

        private void HandleGesture(System.Collections.Generic.List<LeanFinger> fingers)
        {
            if (_ignoreGui && LeanTouch.GuiInUse) return;

            if (fingers == null || fingers.Count < 2)
            {
                _isPinching = false;
                return;
            }

            var pinchRatio = LeanGesture.GetPinchRatio(fingers);
            if (Mathf.Abs(pinchRatio - 1f) <= 0.0001f)
            {
                _isPinching = false;
                return;
            }

            _isPinching = true;
            _targetOrbitRadius *= pinchRatio;
            _targetOrbitRadius = Mathf.Clamp(
                _targetOrbitRadius,
                _minOrbitRadius - _zoomElasticOvershoot,
                _maxOrbitRadius + _zoomElasticOvershoot
            );
        }

        private void ApplyElasticBounds()
        {
            if (_isDragging || _isPinching) return;

            _targetHeight = ElasticReturn(_targetHeight, _minHeight, _maxHeight, _elasticReturnSpeed);
            _targetOrbitRadius = ElasticReturn(_targetOrbitRadius, _minOrbitRadius, _maxOrbitRadius, _zoomElasticReturnSpeed);
        }

        private static float ElasticReturn(float value, float min, float max, float speed)
        {
            if (value < min)
                return Mathf.Lerp(value, min, Time.deltaTime * speed);

            if (value > max)
                return Mathf.Lerp(value, max, Time.deltaTime * speed);

            return value;
        }

        private void SmoothValues()
        {
            _currentAngle = Mathf.LerpAngle(_currentAngle, _targetAngle, _horizontalSmoothing);
            _currentHeight = Mathf.Lerp(_currentHeight, _targetHeight, _verticalSmoothing);
            _currentOrbitRadius = Mathf.Lerp(_currentOrbitRadius, _targetOrbitRadius, _zoomSmoothing);
        }

        private void ApplyCamera()
        {
            Vector3 pivotPosition = GetPivotPosition();
            Vector3 orbitOffset = GetOrbitOffset(_currentAngle, _currentOrbitRadius);

            _cameraTransform.position = pivotPosition + orbitOffset + Vector3.up * _currentHeight;
            _cameraTransform.rotation = Quaternion.Euler(_isometricPitch, _currentAngle, 0f);
        }

        private Vector3 GetPivotPosition()
        {
            Vector3 pivot = _target.position;
            pivot.y += _pivotHeightOffset;
            return pivot;
        }

        private Vector3 GetOrbitOffset(float angle, float radius)
        {
            return Quaternion.Euler(_isometricPitch, angle, 0f) * (Vector3.back * radius);
        }

        private void OnValidate()
        {
            if (_minHeight > _maxHeight) _maxHeight = _minHeight;
            if (_minOrbitRadius > _maxOrbitRadius) _maxOrbitRadius = _minOrbitRadius;
            if (_zoomElasticOvershoot < 0f) _zoomElasticOvershoot = 0f;
            if (_zoomElasticReturnSpeed < 0f) _zoomElasticReturnSpeed = 0f;
        }
    }
}