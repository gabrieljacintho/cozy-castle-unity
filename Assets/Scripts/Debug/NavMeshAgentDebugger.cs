using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAgentDebugger : MonoBehaviour
{
    private NavMeshAgent agent;

    private bool IsAgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    #region Movement Parameters
    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Velocity")]
    public Vector3 Velocity => IsAgentReady ? agent.velocity : Vector3.zero;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Speed (Current)")]
    public float CurrentSpeed => IsAgentReady ? agent.velocity.magnitude : 0f;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Speed (Max)")]
    public float MaxSpeed => agent != null ? agent.speed : 0f;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Angular Speed")]
    public float AngularSpeed => agent != null ? agent.angularSpeed : 0f;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Acceleration")]
    public float Acceleration => agent != null ? agent.acceleration : 0f;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Desired Velocity")]
    public Vector3 DesiredVelocity => IsAgentReady ? agent.desiredVelocity : Vector3.zero;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Next Position")]
    public Vector3 NextPosition => IsAgentReady ? agent.nextPosition : Vector3.zero;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Is Stopped")]
    public bool IsStopped => IsAgentReady && agent.isStopped;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Updating Position")]
    public bool UpdatePosition => agent != null && agent.updatePosition;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Updating Rotation")]
    public bool UpdateRotation => agent != null && agent.updateRotation;

    [TitleGroup("Movement")]
    [ReadOnly, ShowInInspector, LabelText("Updating Up Axis")]
    public bool UpdateUpAxis => agent != null && agent.updateUpAxis;
    #endregion

    #region Path Parameters
    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Has Path")]
    public bool HasPath => IsAgentReady && agent.hasPath;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Path Pending")]
    public bool PathPending => IsAgentReady && agent.pathPending;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Path Stale")]
    public bool IsPathStale => IsAgentReady && agent.isPathStale;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Path Status")]
    public NavMeshPathStatus PathStatus => IsAgentReady && agent.hasPath ? agent.pathStatus : NavMeshPathStatus.PathInvalid;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Destination")]
    public Vector3 Destination => IsAgentReady && agent.hasPath ? agent.destination : Vector3.zero;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Remaining Distance")]
    public float RemainingDistance => IsAgentReady ? agent.remainingDistance : 0f;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Stopping Distance")]
    public float StoppingDistance => agent != null ? agent.stoppingDistance : 0f;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Auto Braking")]
    public bool AutoBraking => agent != null && agent.autoBraking;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Auto Repath")]
    public bool AutoRepath => agent != null && agent.autoRepath;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Steering Target")]
    public Vector3 SteeringTarget => IsAgentReady && agent.hasPath ? agent.steeringTarget : Vector3.zero;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Path Corners Count")]
    public int PathCornersCount => IsAgentReady && agent.hasPath ? agent.path.corners.Length : 0;

    [TitleGroup("Path")]
    [ReadOnly, ShowInInspector, LabelText("Path Corners")]
    public Vector3[] PathCorners => IsAgentReady && agent.hasPath ? agent.path.corners : new Vector3[0];
    #endregion

    #region Agent Properties
    [TitleGroup("Agent Properties")]
    [ReadOnly, ShowInInspector, LabelText("Agent Type ID")]
    public int AgentTypeID => agent != null ? agent.agentTypeID : 0;

    [TitleGroup("Agent Properties")]
    [ReadOnly, ShowInInspector, LabelText("Radius")]
    public float Radius => agent != null ? agent.radius : 0f;

    [TitleGroup("Agent Properties")]
    [ReadOnly, ShowInInspector, LabelText("Height")]
    public float Height => agent != null ? agent.height : 0f;

    [TitleGroup("Agent Properties")]
    [ReadOnly, ShowInInspector, LabelText("Base Offset")]
    public float BaseOffset => agent != null ? agent.baseOffset : 0f;
    #endregion

    #region Obstacle Avoidance
    [TitleGroup("Obstacle Avoidance")]
    [ReadOnly, ShowInInspector, LabelText("Avoidance Priority")]
    public int AvoidancePriority => agent != null ? agent.avoidancePriority : 0;

    [TitleGroup("Obstacle Avoidance")]
    [ReadOnly, ShowInInspector, LabelText("Obstacle Avoidance Type")]
    public ObstacleAvoidanceType ObstacleAvoidanceType => agent != null ? agent.obstacleAvoidanceType : ObstacleAvoidanceType.NoObstacleAvoidance;
    #endregion

    #region NavMesh State
    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Is Active And Enabled")]
    public bool IsActiveAndEnabled => agent != null && agent.isActiveAndEnabled;

    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Is On NavMesh")]
    public bool IsOnNavMesh => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Is On OffMesh Link")]
    public bool IsOnOffMeshLink => IsAgentReady && agent.isOnOffMeshLink;

    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Auto Traverse OffMesh Link")]
    public bool AutoTraverseOffMeshLink => agent != null && agent.autoTraverseOffMeshLink;

    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Area Mask")]
    public int AreaMask => agent != null ? agent.areaMask : 0;

    [TitleGroup("NavMesh State")]
    [ReadOnly, ShowInInspector, LabelText("Is On Valid NavMesh Area")]
    public bool IsOnValidArea
    {
        get
        {
            if (!IsAgentReady) return false;
            NavMeshHit hit;
            return NavMesh.SamplePosition(agent.transform.position, out hit, 0.1f, agent.areaMask);
        }
    }
    #endregion

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnValidate()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }
}