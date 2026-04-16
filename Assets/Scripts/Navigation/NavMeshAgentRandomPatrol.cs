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
        private string _activeIntParameterName;
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

            ClearActiveParameters();
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

                ClearActiveParameters();
                
                waypoint.GenerateRandomValue();
                _activeBoolParameterName = waypoint.AnimatorBoolParameterName;
                _activeIntParameterName = waypoint.AnimatorIntParameterName;
                
                if (!string.IsNullOrEmpty(_activeBoolParameterName))
                {
                    _animator.SetBool(_activeBoolParameterName, true);
                }
                
                if (!string.IsNullOrEmpty(_activeIntParameterName))
                {
                    _animator.SetInteger(_activeIntParameterName, waypoint.CurrentRandomValue);
                }

                float stayDuration = Random.Range(_minStaySeconds, _maxStaySeconds);
                yield return new WaitForSeconds(stayDuration);

                ClearActiveParameters();
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
            bool closeEnoughByRemaining = false;
            if (_agent.pathStatus != NavMeshPathStatus.PathInvalid && !_agent.pathPending)
            {
                closeEnoughByRemaining = _agent.remainingDistance <= _agent.stoppingDistance;
            }
            
            float distanceToDestination = Vector3.Distance(_agent.transform.position, _agent.destination);
            bool closeEnoughByPosition = distanceToDestination <= _agent.stoppingDistance;

            return closeEnoughByRemaining || closeEnoughByPosition;
        }

        private void ClearActiveParameters()
        {
            if (_animator == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_activeBoolParameterName))
            {
                _animator.SetBool(_activeBoolParameterName, false);
                _activeBoolParameterName = null;
            }

            if (!string.IsNullOrEmpty(_activeIntParameterName))
            {
                _animator.SetInteger(_activeIntParameterName, -1);
                _activeIntParameterName = null;
            }

            foreach (NavMeshWaypoint waypoint in _waypoints)
            {
                if (waypoint != null)
                {
                    waypoint.ResetRandomValue();
                }
            }
        }
    }
}
