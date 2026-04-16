using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;

namespace GabrielBertasso.Animation
{
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimatorController : MonoBehaviour
    {
        [Serializable]
        public struct VelocityThreshold
        {
            public float Velocity;
            public float Value;
        }

        public enum VelocitySource
        {
            NavMeshAgent,
            CharacterController,
            Rigidbody
        }

        public enum BlendType
        {
            OneD,
            TwoD
        }

        [Title("Velocity Source")]
        [SerializeField] private VelocitySource _velocitySource = VelocitySource.CharacterController;

        [Title("Blend Type")]
        [SerializeField] private BlendType _blendType = BlendType.OneD;

        [Title("Animator Parameters")]
        [SerializeField, ShowIf("@_blendType == BlendType.OneD")] 
        private string _velocityParameterName = "Speed";

        [SerializeField, ShowIf("@_blendType == BlendType.TwoD")] 
        private string _verticalParameterName = "MoveY";

        [SerializeField, ShowIf("@_blendType == BlendType.TwoD")] 
        private string _horizontalParameterName = "MoveX";

        [SerializeField] private string _isMovingParameterName = "IsMoving";

        [Title("Movement Settings")]
        [SerializeField, MinValue(0f)] 
        private float _movementThreshold = 0.1f;

        [SerializeField, ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "Velocity")]
        private List<VelocityThreshold> _velocityThresholds = new List<VelocityThreshold>
        {
            new VelocityThreshold { Velocity = 0f, Value = 0f },
            new VelocityThreshold { Velocity = 1f, Value = 1f },
            new VelocityThreshold { Velocity = 5f, Value = 2f }
        };

        private Animator _animator;
        private NavMeshAgent _navMeshAgent;
        private CharacterController _characterController;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            switch (_velocitySource)
            {
                case VelocitySource.NavMeshAgent:
                    _navMeshAgent = GetComponent<NavMeshAgent>();
                    break;
                case VelocitySource.CharacterController:
                    _characterController = GetComponent<CharacterController>();
                    break;
                case VelocitySource.Rigidbody:
                    _rigidbody = GetComponent<Rigidbody>();
                    break;
            }
        }

        private void Update()
        {
            Vector3 velocity = GetVelocity();
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float velocityMagnitude = velocity.magnitude;

            bool isMoving = velocityMagnitude >= _movementThreshold;
            _animator.SetBool(_isMovingParameterName, isMoving);

            switch (_blendType)
            {
                case BlendType.OneD:
                    UpdateOneDBlend(velocityMagnitude);
                    break;
                case BlendType.TwoD:
                    UpdateTwoDBlend(localVelocity);
                    break;
            }
        }

        private Vector3 GetVelocity()
        {
            switch (_velocitySource)
            {
                case VelocitySource.NavMeshAgent:
                    return _navMeshAgent != null ? _navMeshAgent.velocity : Vector3.zero;
                case VelocitySource.CharacterController:
                    return _characterController != null ? _characterController.velocity : Vector3.zero;
                case VelocitySource.Rigidbody:
                    return _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
                default:
                    return Vector3.zero;
            }
        }

        private void UpdateOneDBlend(float velocityMagnitude)
        {
            float lerpedValue = GetLerpedValue(velocityMagnitude);
            _animator.SetFloat(_velocityParameterName, lerpedValue);
        }

        private void UpdateTwoDBlend(Vector3 localVelocity)
        {
            float forwardVelocity = localVelocity.z;
            float rightVelocity = localVelocity.x;

            float forwardValue = GetLerpedValue(Mathf.Abs(forwardVelocity)) * Mathf.Sign(forwardVelocity);
            float rightValue = GetLerpedValue(Mathf.Abs(rightVelocity)) * Mathf.Sign(rightVelocity);

            _animator.SetFloat(_verticalParameterName, forwardValue);
            _animator.SetFloat(_horizontalParameterName, rightValue);
        }

        private float GetLerpedValue(float velocity)
        {
            if (_velocityThresholds == null || _velocityThresholds.Count == 0)
                return 0f;

            if (_velocityThresholds.Count == 1)
                return _velocityThresholds[0].Value;

            for (int i = 0; i < _velocityThresholds.Count - 1; i++)
            {
                VelocityThreshold current = _velocityThresholds[i];
                VelocityThreshold next = _velocityThresholds[i + 1];

                if (velocity >= current.Velocity && velocity <= next.Velocity)
                {
                    float range = next.Velocity - current.Velocity;
                    if (range <= 0f)
                        return current.Value;

                    float t = (velocity - current.Velocity) / range;
                    return Mathf.Lerp(current.Value, next.Value, t);
                }
            }

            if (velocity <= _velocityThresholds[0].Velocity)
                return _velocityThresholds[0].Value;

            return _velocityThresholds[_velocityThresholds.Count - 1].Value;
        }

        private void OnValidate()
        {
            if (_velocityThresholds != null && _velocityThresholds.Count > 1)
            {
                for (int i = 1; i < _velocityThresholds.Count; i++)
                {
                    if (_velocityThresholds[i].Velocity < _velocityThresholds[i - 1].Velocity)
                    {
                        VelocityThreshold threshold = _velocityThresholds[i];
                        threshold.Velocity = _velocityThresholds[i - 1].Velocity;
                        _velocityThresholds[i] = threshold;
                    }
                }
            }
        }
    }
}
