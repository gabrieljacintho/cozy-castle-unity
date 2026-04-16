using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

namespace GabrielBertasso.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class RootMotionNavAgent : MonoBehaviour
    {
        [Header("Animator Parameters")]
        [Tooltip("Name of the float parameter driving the strafe axis (left/right).")]
        [SerializeField] private string _moveXParameter = "MoveX";

        [Tooltip("Name of the float parameter driving the forward/backward axis.")]
        [SerializeField] private string _moveYParameter = "MoveY";

        [Header("Movement Tuning")]
        [Tooltip("Time used to smoothly interpolate the animator parameters, avoiding sudden blend switches.")]
        [Range(0f, 1f)]
        [SerializeField] private float _parameterSmoothTime = 0.15f;

        [Tooltip("Distance under which the agent is considered to have arrived and animation returns to idle.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float _arrivalThreshold = 0.2f;

        [Tooltip("Reference speed (m/s) of the forward walk animation. Used as a fallback before the real root motion speed is measured. Set this close to your actual animation speed to avoid a warm-up frame.")]
        [Range(0.1f, 20f)]
        [SerializeField] private float _referenceAnimationSpeed = 2f;

        [Tooltip("Smoothing time used to stabilize the measured root motion speed across frames.")]
        [Range(0f, 1f)]
        [SerializeField] private float _measuredSpeedSmoothTime = 0.1f;

        private NavMeshAgent _agent;
        private Animator _animator;

        private int _moveXHash;
        private int _moveYHash;

        private float _currentMoveX;
        private float _currentMoveY;
        private float _moveXVelocity;
        private float _moveYVelocity;

        private float _measuredSpeed;
        private float _measuredSpeedVelocity;

        [ShowInInspector, ReadOnly]
        public bool HasPath => _agent != null && _agent.hasPath;

        [ShowInInspector, ReadOnly]
        public float RemainingDistance => _agent != null ? _agent.remainingDistance : 0f;

        [ShowInInspector, ReadOnly]
        public float MeasuredAnimationSpeed => _measuredSpeed;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();

            _moveXHash = Animator.StringToHash(_moveXParameter);
            _moveYHash = Animator.StringToHash(_moveYParameter);

            // Root Motion drives translation; NavMeshAgent only plans the path and steers rotation.
            _agent.updatePosition = false;
            _agent.updateRotation = true;

            _measuredSpeed = _referenceAnimationSpeed;
            _agent.speed = _referenceAnimationSpeed;
        }

        private void Update()
        {
            UpdateAnimatorParameters();
        }

        private void OnAnimatorMove()
        {
            if (_agent == null || _animator == null)
            {
                return;
            }

            // Measure the actual speed produced by the animation's root motion this frame.
            float deltaTime = Time.deltaTime;

            if (deltaTime > Mathf.Epsilon)
            {
                float frameSpeed = _animator.deltaPosition.magnitude / deltaTime;
                _measuredSpeed = Mathf.SmoothDamp(_measuredSpeed, frameSpeed, ref _measuredSpeedVelocity, _measuredSpeedSmoothTime);
            }

            // Keep the NavMeshAgent's internal speed aligned with what the animation is actually delivering,
            // so desiredVelocity and path evaluation stay consistent with the visual motion.
            if (_measuredSpeed > 0.01f)
            {
                _agent.speed = _measuredSpeed;
            }

            // Apply the root motion translation and keep the agent pinned to the NavMesh.
            Vector3 rootPosition = transform.position + _animator.deltaPosition;
            rootPosition.y = _agent.nextPosition.y;

            transform.position = rootPosition;
            _agent.nextPosition = rootPosition;
        }

        private void UpdateAnimatorParameters()
        {
            Vector2 targetInput = CalculateLocalMoveInput();

            _currentMoveX = Mathf.SmoothDamp(_currentMoveX, targetInput.x, ref _moveXVelocity, _parameterSmoothTime);
            _currentMoveY = Mathf.SmoothDamp(_currentMoveY, targetInput.y, ref _moveYVelocity, _parameterSmoothTime);

            _animator.SetFloat(_moveXHash, _currentMoveX);
            _animator.SetFloat(_moveYHash, _currentMoveY);
        }

        private Vector2 CalculateLocalMoveInput()
        {
            if (!_agent.hasPath || _agent.remainingDistance <= _arrivalThreshold)
            {
                return Vector2.zero;
            }

            Vector3 desired = _agent.desiredVelocity;

            if (desired.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            // Convert the world-space steering direction into the character's local space
            // so we know if it needs to walk forward, back, left or right relative to itself.
            Vector3 localDirection = transform.InverseTransformDirection(desired.normalized);
            Vector2 planar = new Vector2(localDirection.x, localDirection.z);

            // The magnitude is already normalized (it's a direction), so the Blend Tree receives
            // pure direction values. The actual speed of movement is handled by the animation itself.
            return Vector2.ClampMagnitude(planar, 1f);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_moveXParameter))
            {
                _moveXParameter = "MoveX";
            }

            if (string.IsNullOrWhiteSpace(_moveYParameter))
            {
                _moveYParameter = "MoveY";
            }
        }
    }
}