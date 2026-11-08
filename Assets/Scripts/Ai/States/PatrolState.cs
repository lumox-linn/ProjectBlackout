using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// PatrolState  (B3, sight check added in B4, hearing added in B5)
//
// Walks between the patrol points in a loop (1 -> 2 -> ... -> 10 -> 1 ...),
// pausing at each one for PatrolWaitTime seconds.
//
// - Enter: set patrol speed, then head to the CLOSEST patrol point
//          (so after a chase it resumes from wherever it ended up).
// - Tick:  wait for the path -> walk -> arrived? pause -> next point.
//          Safety nets: skip a point that can't be reached, and skip a point
//          if the creature hasn't moved for StuckTimeout seconds.
// - Exit:  clear the pause so the next Enter starts fresh.
//
// Waypoint patrol adapted from the Unity AI Navigation manual,
// "Make an agent patrol between a set of points":
//   https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavAgentPatrol.html
// (Our additions: pause at each point, resume at the closest point,
//  unreachable-point and stuck detection.)
//
// Transitions (checked first in Tick, in this order):
//   Patrol -> Chase        sees the player                 (B4)
//   Patrol -> Investigate  hears a noise within its radius (B5)
// ---------------------------------------------------------------------------
public class PatrolState : CreatureState
{
    // How close (beyond the agent's Stopping Distance) counts as "arrived".
    private const float ArriveTolerance = 0.3f;

    // If the creature moves slower than this (m/s), it counts as "not moving".
    private const float StuckSpeed = 0.1f;

    // If a point can't be reached, wait this long before trying the next one.
    private const float RetryDelay = 0.5f;

    private int targetIndex = -1;   // which patrol point we're heading to
    private bool isWaiting;         // true while pausing at a point
    private float waitTimer;        // seconds left of the pause
    private float stuckTimer;       // seconds spent not moving
    private bool pathChecked;       // have we checked the new path's status yet?

    public PatrolState(CreatureController creature) : base(creature) { }

    public override string Name => "Patrol";

    public override void Enter()
    {
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.PatrolSpeed;
        agent.isStopped = false;
        isWaiting = false;

        if (creature.PatrolPoints.Count == 0)
        {
            Debug.LogWarning("[Patrol] No patrol points, so the creature will stand still.", creature);
            agent.ResetPath();
            return;
        }

        targetIndex = FindClosestPointIndex();
        GoToCurrentPoint();
    }

    public override void Tick()
    {
        // Transition: Patrol -> Chase when the creature sees the player.
        if (creature.Senses.CanSeePlayer)
        {
            creature.Fsm.ChangeState(creature.Chase);
            return;
        }

        // Transition: Patrol -> Investigate when the creature hears a noise.
        if (creature.Senses.TryConsumeNoise(out Vector3 noisePosition))
        {
            creature.Investigate.SetTarget(noisePosition, "noise");
            creature.Fsm.ChangeState(creature.Investigate);
            return;
        }

        if (creature.PatrolPoints.Count == 0)
        {
            return;
        }

        NavMeshAgent agent = creature.Agent;

        // 1) Pausing at a point (or waiting before a retry): count down, then move on.
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                GoToNextPoint();
            }
            return;
        }

        // 2) Unity is still calculating the path: wait.
        if (agent.pathPending)
        {
            return;
        }

        // 3) Once per new path: can we actually get there?
        if (!pathChecked)
        {
            pathChecked = true;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[Patrol] Can't find a path to {CurrentPoint.name}. Skipping it.", CurrentPoint);
                StartWaiting(RetryDelay);
                return;
            }

            if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning($"[Patrol] {CurrentPoint.name} can't be fully reached " +
                                 "(closed door?). Going as close as possible.", CurrentPoint);
            }
        }

        // 4) Arrived? Pause here.
        if (agent.remainingDistance <= agent.stoppingDistance + ArriveTolerance)
        {
            if (creature.LogPatrolPoints)
            {
                Debug.Log($"[Patrol] Reached {CurrentPoint.name} ({targetIndex + 1}/{creature.PatrolPoints.Count})", creature);
            }
            StartWaiting(creature.PatrolWaitTime);
            return;
        }

        // 5) Stuck check: not moving for too long on the way? Skip to the next point.
        if (agent.velocity.magnitude < StuckSpeed)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= creature.StuckTimeout)
            {
                Debug.LogWarning($"[Patrol] Stuck on the way to {CurrentPoint.name}. Skipping to the next point.", creature);
                GoToNextPoint();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    public override void Exit()
    {
        isWaiting = false;
    }

    // --- Helpers ---

    private Transform CurrentPoint => creature.PatrolPoints[targetIndex];

    private void GoToNextPoint()
    {
        // % (modulo) wraps around: after the last point, go back to point 0.
        targetIndex = (targetIndex + 1) % creature.PatrolPoints.Count;
        GoToCurrentPoint();
    }

    private void GoToCurrentPoint()
    {
        pathChecked = false;
        stuckTimer = 0f;

        bool requested = creature.Agent.SetDestination(CurrentPoint.position);
        if (!requested)
        {
            // The point isn't near the NavMesh at all.
            Debug.LogWarning($"[Patrol] {CurrentPoint.name} is not on the NavMesh. Skipping it.", CurrentPoint);
            StartWaiting(RetryDelay);
        }
    }

    private void StartWaiting(float seconds)
    {
        isWaiting = true;
        waitTimer = seconds;
    }

    // Straight-line distance check. sqrMagnitude avoids a square root, so it's cheaper,
    // and it's fine for comparing which distance is smallest.
    private int FindClosestPointIndex()
    {
        Vector3 myPosition = creature.transform.position;
        int closestIndex = 0;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < creature.PatrolPoints.Count; i++)
        {
            float sqrDistance = (creature.PatrolPoints[i].position - myPosition).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}
