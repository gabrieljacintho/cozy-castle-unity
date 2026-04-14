using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace GabrielBertasso.Navigation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class NavMeshAgentRandomPatrol : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Animator whose bool parameters match each waypoint name.")]
        private Animator _animator;

        [SerializeField, Tooltip("Waypoints the agent can travel to.")]
        private NavMeshWaypoint[] _waypoints;

        [Header("Timing")]
        [SerializeField, Tooltip("Minimum seconds to stay at a point after arrival.")]
        private float _minStaySeconds = 2f;

        [SerializeField, Tooltip("Maximum seconds to stay at a point after arrival.")]
        private float _maxStaySeconds = 5f;

        private NavMeshAgent _agent;
        private string _activeBoolParameterName;
        private Coroutine _patrolRoutine;
        private bool _hasReachedDestination;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                Debug.LogError("NavMeshAgentRandomPatrol requires at least one NavMeshWaypoint.", this);
                return;
            }

            if (_animator == null)
            {
                Debug.LogError("NavMeshAgentRandomPatrol requires an Animator reference.", this);
                return;
            }

            _patrolRoutine = StartCoroutine(PatrolLoop());
        }

        private void OnDisable()
        {
            if (_patrolRoutine != null)
            {
                StopCoroutine(_patrolRoutine);
                _patrolRoutine = null;
            }

            ClearActiveBool();
        }

        private void OnValidate()
        {
            if (_minStaySeconds < 0f)
            {
                _minStaySeconds = 0f;
            }

            if (_maxStaySeconds < _minStaySeconds)
            {
                _maxStaySeconds = _minStaySeconds;
            }
        }

        private IEnumerator PatrolLoop()
        {
            while (enabled)
            {
                NavMeshWaypoint waypoint = _waypoints[Random.Range(0, _waypoints.Length)];

                _agent.SetDestination(waypoint.transform.position);
                yield return WaitUntilDestinationReached();

                if (!_hasReachedDestination)
                {
                    yield return null;
                    continue;
                }

                ClearActiveBool();
                _activeBoolParameterName = waypoint.AnimatorBoolParameterName;
                if (!string.IsNullOrEmpty(_activeBoolParameterName))
                {
                    _animator.SetBool(_activeBoolParameterName, true);
                }

                float stayDuration = Random.Range(_minStaySeconds, _maxStaySeconds);
                yield return new WaitForSeconds(stayDuration);

                ClearActiveBool();
            }
        }

        private IEnumerator WaitUntilDestinationReached()
        {
            _hasReachedDestination = false;
            yield return null;

            if (IsConsideredArrivedAtDestination())
            {
                _hasReachedDestination = true;
                yield break;
            }

            while (enabled && _agent != null && _agent.isOnNavMesh)
            {
                if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    yield break;
                }

                if (IsConsideredArrivedAtDestination())
                {
                    _hasReachedDestination = true;
                    yield break;
                }

                yield return null;
            }
        }

        private bool IsConsideredArrivedAtDestination()
        {
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }

            if (_agent.pathPending)
            {
                return false;
            }

            bool closeEnoughByRemaining = _agent.remainingDistance <= _agent.stoppingDistance;

            float distanceToDestination = Vector3.Distance(_agent.transform.position, _agent.destination);
            bool closeEnoughByPosition = distanceToDestination <= _agent.stoppingDistance;

            if (!closeEnoughByRemaining && !closeEnoughByPosition)
            {
                return false;
            }

            return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f;
        }

        private void ClearActiveBool()
        {
            if (string.IsNullOrEmpty(_activeBoolParameterName) || _animator == null)
            {
                return;
            }

            _animator.SetBool(_activeBoolParameterName, false);
            _activeBoolParameterName = null;
        }
    }
}
