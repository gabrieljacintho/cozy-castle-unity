using UnityEngine;
using Sirenix.OdinInspector;

namespace GabrielBertasso.Animation
{
    /// <summary>
    /// Helper component to configure Animator for root motion movement.
    /// This script provides an easy way to set up the required parameters.
    /// </summary>
    public class RootMotionAnimatorSetup : MonoBehaviour
    {
        [Header("Animator Configuration")]
        [SerializeField, Tooltip("The Animator component to configure.")]
        private Animator _animator;

        [Header("Animation Clips")]
        [SerializeField, Tooltip("Animation for moving forward.")]
        private AnimationClip _forwardAnimation;

        [SerializeField, Tooltip("Animation for moving backward.")]
        private AnimationClip _backwardAnimation;

        [SerializeField, Tooltip("Animation for moving left.")]
        private AnimationClip _leftAnimation;

        [SerializeField, Tooltip("Animation for moving right.")]
        private AnimationClip _rightAnimation;

        [SerializeField, Tooltip("Idle animation.")]
        private AnimationClip _idleAnimation;

        [Header("Parameter Names")]
        [SerializeField, Tooltip("Parameter name for forward/backward movement.")]
        private string _forwardParameterName = "MoveY";

        [SerializeField, Tooltip("Parameter name for left/right movement.")]
        private string _sidewaysParameterName = "MoveX";

        [SerializeField, Tooltip("Parameter name for movement state.")]
        private string _isMovingParameterName = "IsMoving";

        
        [Button("Validate Setup")]
        private void ValidateSetup()
        {
            if (_animator == null)
            {
                Debug.LogError("Animator reference is missing.", this);
                return;
            }

            bool hasRequiredParameters = true;

            // Check forward parameter
            if (!_animator.HasParameterOfType(_forwardParameterName, AnimatorControllerParameterType.Float))
            {
                Debug.LogWarning($"Missing float parameter: {_forwardParameterName}", this);
                hasRequiredParameters = false;
            }

            // Check sideways parameter
            if (!_animator.HasParameterOfType(_sidewaysParameterName, AnimatorControllerParameterType.Float))
            {
                Debug.LogWarning($"Missing float parameter: {_sidewaysParameterName}", this);
                hasRequiredParameters = false;
            }

            // Check is moving parameter
            if (!_animator.HasParameterOfType(_isMovingParameterName, AnimatorControllerParameterType.Bool))
            {
                Debug.LogWarning($"Missing bool parameter: {_isMovingParameterName}", this);
                hasRequiredParameters = false;
            }

            if (hasRequiredParameters)
            {
                Debug.Log("Animator setup is valid! All required parameters are present.", this);
            }

            // Check root motion settings
            if (_animator != null)
            {
                var controller = _animator.runtimeAnimatorController;
                if (controller != null)
                {
                    Debug.Log($"Animator Controller: {controller.name}", this);
                }
                else
                {
                    Debug.LogWarning("No Animator Controller assigned.", this);
                }
            }
        }

        [Button("Test Animation Parameters")]
        private void TestAnimationParameters()
        {
            if (_animator == null)
            {
                Debug.LogError("Animator reference is missing.", this);
                return;
            }

            // Test forward movement
            Debug.Log($"Testing {_forwardParameterName} = 1.0 (forward)");
            _animator.SetFloat(_forwardParameterName, 1.0f);
            _animator.SetFloat(_sidewaysParameterName, 0.0f);
            _animator.SetBool(_isMovingParameterName, true);

            // Reset after delay
            StartCoroutine(ResetParameters());
        }

        private System.Collections.IEnumerator ResetParameters()
        {
            yield return new WaitForSeconds(2.0f);

            if (_animator != null)
            {
                Debug.Log("Resetting parameters to idle");
                _animator.SetFloat(_forwardParameterName, 0.0f);
                _animator.SetFloat(_sidewaysParameterName, 0.0f);
                _animator.SetBool(_isMovingParameterName, false);
            }
        }

        private void OnValidate()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
        }

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// Extension methods for Animator parameter checking.
    /// </summary>
    public static class AnimatorExtensions
    {
        public static bool HasParameterOfType(this Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name))
                return false;

            foreach (var param in animator.parameters)
            {
                if (param.name == name && param.type == type)
                    return true;
            }
            return false;
        }
    }
}
