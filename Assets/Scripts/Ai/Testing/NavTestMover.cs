using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// test helper: makes the NavMeshAgent walk to a target, to check the creature paths around walls
// (the pathfinding itself, an A* search over the NavMesh, is done by Unity; we only call SetDestination)
// Unity Scripting API: https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html
[RequireComponent(typeof(NavMeshAgent))]
public class NavTestMover : MonoBehaviour
{
    // where the creature should walk to (drag the PlayerStandIn here)
    [SerializeField] private Transform target;
    // how often (seconds) we check whether the target moved (cheaper than every frame)
    public float repathInterval = 0.25f;
    // only ask for a new path if the target moved more than this many metres
    public float repathDistance = 0.5f;

    // the agent that does the pathfinding and moving
    private NavMeshAgent agent;
    // where the target was when we last asked for a path
    private Vector3 lastTargetPosition;
    // counts down to the next check
    private float repathTimer = 0.0f;
    // makes sure each message is only logged once per path
    private bool pathStatusLogged = false;
    private bool arrivedLogged = false;

    void Awake()
    {
        // get the agent once instead of every frame
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // no target set
        if (target == null)
        {
            Debug.LogWarning("[NavTestMover] No target set on " + name + ". Drag a Transform into the Target slot.", this);
            enabled = false;
            return;
        }

        // not standing on the NavMesh
        if (!agent.isOnNavMesh)
        {
            Debug.LogError("[NavTestMover] " + name + " is not on the NavMesh. Move it onto the blue NavMesh area, and check the Agent Type matches the NavMeshSurface.", this);
            enabled = false;
            return;
        }

        SendToTarget();
    }

    void Update()
    {
        // 1) every repathInterval seconds, ask for a new path if the target has moved
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0.0f)
        {
            repathTimer = repathInterval;
            float movedSqr = (target.position - lastTargetPosition).sqrMagnitude;
            if (movedSqr > repathDistance * repathDistance)
            {
                SendToTarget();
            }
        }

        // Unity is still calculating the path, so wait
        if (agent.pathPending)
        {
            return;
        }

        // 2) once the path is calculated, report whether it reaches the target
        if (!pathStatusLogged)
        {
            pathStatusLogged = true;

            if (agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                Debug.Log("[NavTestMover] Path found: " + agent.path.corners.Length + " corners, " + agent.remainingDistance.ToString("0.0") + " m to go.", this);
            }
            else if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning("[NavTestMover] Target can't be fully reached (probably behind a closed door). Walking as close as possible.", this);
            }
            else
            {
                Debug.LogWarning("[NavTestMover] No valid path to the target.", this);
            }
        }

        // 3) log once when the creature arrives (within its Stopping Distance)
        if (!arrivedLogged && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            arrivedLogged = true;
            Debug.Log("[NavTestMover] Arrived at the target.", this);
        }
    }

    // asks Unity to calculate a path to the target (the agent then walks it by itself)
    private void SendToTarget()
    {
        lastTargetPosition = target.position;
        pathStatusLogged = false;
        arrivedLogged = false;
        agent.SetDestination(target.position);
    }

    // draws the current path as a cyan line in the Scene view (Play mode only)
    void OnDrawGizmos()
    {
        if (agent == null || !agent.hasPath)
        {
            return;
        }

        Vector3[] corners = agent.path.corners;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Gizmos.DrawLine(corners[i], corners[i + 1]);
            Gizmos.DrawSphere(corners[i + 1], 0.1f);
        }
    }
}
