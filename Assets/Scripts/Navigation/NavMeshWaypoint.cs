using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;

namespace GabrielBertasso.Navigation
{
    public class NavMeshWaypoint : MonoBehaviour
    {
        [SerializeField, Tooltip("Animator bool parameter name to set true while the agent is stopped at this point.")]
        private string _animatorBoolParameterName;

        [Header("Random Integer Parameter")]
        [SerializeField, Tooltip("Minimum value for the random integer parameter.")]
        private int _minRandomValue = 0;

        [SerializeField, Tooltip("Maximum value for the random integer parameter.")]
        private int _maxRandomValue = 2;

        [SerializeField, Tooltip("Name of the integer parameter in the animator.")]
        private string _animatorIntParameterName;

        private int _currentRandomValue = -1;

        public string AnimatorBoolParameterName
        {
            get
            {
                return _animatorBoolParameterName;
            }
        }

        public string AnimatorIntParameterName
        {
            get
            {
                return _animatorIntParameterName;
            }
        }

        public int CurrentRandomValue
        {
            get
            {
                return _currentRandomValue;
            }
        }

        public void GenerateRandomValue()
        {
            _currentRandomValue = Random.Range(_minRandomValue, _maxRandomValue + 1);
        }

        public void ResetRandomValue()
        {
            _currentRandomValue = -1;
        }

        [Button("Position On NavMesh")]
        private void PositionOnNavMesh()
        {
            Vector3 currentPosition = transform.position;
            NavMeshHit hit;
            
            if (NavMesh.SamplePosition(currentPosition, out hit, 10f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                Debug.Log($"Transform positioned on NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogWarning("Could not find valid NavMesh position near current location");
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_animatorBoolParameterName))
            {
                return;
            }

            _animatorBoolParameterName = _animatorBoolParameterName.Trim();

            if (string.IsNullOrWhiteSpace(_animatorIntParameterName))
            {
                return;
            }

            _animatorIntParameterName = _animatorIntParameterName.Trim();

            if (_maxRandomValue < _minRandomValue)
            {
                _maxRandomValue = _minRandomValue;
            }
        }
    }
}
