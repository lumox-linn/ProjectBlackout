using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// NavTestMover  (Task B1 – test helper, TEMPORARY)
//
// Makes the creature's NavMeshAgent walk to a target Transform (for example the
// PlayerStandIn capsule). It proves three things:
//   1. The creature is standing on the baked NavMesh.
//   2. Unity's pathfinding routes it AROUND walls and through doorways.
//   3. Closed doors (carving NavMeshObstacles) block the path.
//
// The pathfinding itself (A* search over the NavMesh polygons) is done by Unity.
// We only call SetDestination(). Reference:
//   https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html
//   https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html
//
// Once the state machine (B2) and Patrol state (B3) work, REMOVE this component
// from the creature. The states will control the agent instead.
// ---------------------------------------------------------------------------
[RequireComponent(typeof(NavMeshAgent))]
public class NavTestMover : MonoBehaviour
{
    [Tooltip("Where the creature should walk to (drag the PlayerStandIn here).")]
    [SerializeField] private Transform target;

    [Tooltip("How often (in seconds) we check whether the target moved. " +
             "Checking a few times per second instead of every frame is cheaper.")]
    [SerializeField] private float repathInterval = 0.25f;

    [Tooltip("Only ask for a new path if the target moved more than this many metres.")]
    [SerializeField] private float repathDistance = 0.5f;

    private NavMeshAgent agent;
    private Vector3 lastTargetPosition;
    private float repathTimer;
    private bool pathStatusLogged;
    private bool arrivedLogged;

    private void Awake()
    {
        // Cache the component once instead of calling GetComponent every frame.
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"[NavTestMover] No target set on {name}. Drag a Transform into the Target slot.", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError($"[NavTestMover] {name} is not on the NavMesh. " +
                           "Move it onto the blue NavMesh area, and check the Agent Type matches the NavMeshSurface.", this);
            enabled = false;
            return;
        }

        SendToTarget();
    }

    private void Update()
    {
        // 1) Every repathInterval seconds, re-send the destination if the target has moved.
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;

            float movedSqr = (target.position - lastTargetPosition).sqrMagnitude;
            if (movedSqr > repathDistance * repathDistance)
            {
                SendToTarget();
            }
        }

        // pathPending = Unity is still calculating the path. Wait until it's done.
        if (agent.pathPending)
        {
            return;
        }

        // 2) Once the path is calculated, report whether it reaches the target.
        if (!pathStatusLogged)
        {
            pathStatusLogged = true;

            if (agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                Debug.Log($"[NavTestMover] Path found: {agent.path.corners.Length} corners, " +
                          $"{agent.remainingDistance:F1} m to go.", this);
            }
            else if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning("[NavTestMover] Target can't be fully reached (probably behind a closed door). " +
                                 "Walking as close as possible.", this);
            }
            else
            {
                Debug.LogWarning("[NavTestMover] No valid path to the target.", this);
            }
        }

        // 3) Log once when the creature arrives (within its Stopping Distance).
        if (!arrivedLogged && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            arrivedLogged = true;
            Debug.Log("[NavTestMover] Arrived at the target.", this);
        }
    }

    private void SendToTarget()
    {
        lastTargetPosition = target.position;
        pathStatusLogged = false;
        arrivedLogged = false;

        // SetDestination asks Unity to calculate a path. The agent then walks it by itself.
        agent.SetDestination(target.position);
    }

    // Draws the current path as a cyan line in the Scene view (Play mode only).
    // Good for screenshots in the report and for the presentation demo.
    private void OnDrawGizmos()
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
