using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// walks between the patrol points in a loop (1 -> 2 -> ... -> 10 -> 1), pausing at each one
// Patrol -> Chase when it sees the player, Patrol -> Investigate when it hears a noise
// waypoint patrol adapted from the Unity AI Navigation manual, "Make an agent patrol between a set of points":
// https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavAgentPatrol.html
// (my additions: pause at each point, resume at the closest point, unreachable-point and stuck checks)
public class PatrolState : CreatureState
{
    // how close (beyond the agent's Stopping Distance) counts as "arrived"
    private const float arriveTolerance = 0.3f;
    // slower than this (m/s) counts as "not moving"
    private const float stuckSpeed = 0.1f;
    // if a point can't be reached, wait this long before trying the next one
    private const float retryDelay = 0.5f;

    // which patrol point we are heading to
    private int targetIndex = -1;
    // true while pausing at a point
    private bool isWaiting = false;
    // seconds left of the pause
    private float waitTimer = 0.0f;
    // seconds spent not moving
    private float stuckTimer = 0.0f;
    // whether the new path's status has been checked yet
    private bool pathChecked = false;

    public PatrolState(CreatureController creature) : base(creature)
    {
    }

    public override string Name
    {
        get { return "Patrol"; }
    }

    // the patrol point we are heading to
    private Transform CurrentPoint
    {
        get { return creature.patrolPoints[targetIndex]; }
    }

    public override void Enter()
    {
        // walk at patrol speed
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.patrolSpeed;
        agent.isStopped = false;
        isWaiting = false;

        // no patrol points: stand still
        if (creature.patrolPoints.Count == 0)
        {
            Debug.LogWarning("[Patrol] No patrol points, so the creature will stand still.", creature);
            agent.ResetPath();
            return;
        }

        // start from the closest point (so after a chase it carries on from where it is)
        targetIndex = FindClosestPointIndex();
        GoToCurrentPoint();
    }

    public override void Tick()
    {
        // Patrol -> Chase when the creature sees the player
        if (creature.Fsm.CanLeaveState && creature.Senses.CanSeePlayer)
        {
            creature.Fsm.ChangeState(creature.Chase);
            return;
        }

        // Patrol -> Investigate when the creature hears a noise
        Vector3 noisePosition;
        if (creature.Fsm.CanLeaveState && creature.Senses.TryConsumeNoise(out noisePosition))
        {
            creature.Investigate.SetTarget(noisePosition, "noise");
            creature.Fsm.ChangeState(creature.Investigate);
            return;
        }

        // nothing to do without patrol points
        if (creature.patrolPoints.Count == 0)
        {
            return;
        }

        NavMeshAgent agent = creature.Agent;

        // 1) pausing at a point (or waiting before a retry): count down, then move on
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0.0f)
            {
                isWaiting = false;
                GoToNextPoint();
            }
            return;
        }

        // 2) Unity is still calculating the path, so wait
        if (agent.pathPending)
        {
            return;
        }

        // 3) once per new path: can we actually get there?
        if (!pathChecked)
        {
            pathChecked = true;

            // no path at all: skip this point
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("[Patrol] Can't find a path to " + CurrentPoint.name + ". Skipping it.", CurrentPoint);
                StartWaiting(retryDelay);
                return;
            }

            // only part of the way (e.g. a closed door): go as close as possible
            if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning("[Patrol] " + CurrentPoint.name + " can't be fully reached (closed door?). Going as close as possible.", CurrentPoint);
            }
        }

        // 4) arrived, so pause here
        if (agent.remainingDistance <= agent.stoppingDistance + arriveTolerance)
        {
            if (creature.logStateDetails)
            {
                Debug.Log("[Patrol] Reached " + CurrentPoint.name + " (" + (targetIndex + 1) + "/" + creature.patrolPoints.Count + ")", creature);
            }
            StartWaiting(creature.patrolWaitTime);
            return;
        }

        // 5) stuck check: not moving for too long on the way, so skip to the next point
        if (agent.velocity.magnitude < stuckSpeed)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= creature.stuckTimeout)
            {
                Debug.LogWarning("[Patrol] Stuck on the way to " + CurrentPoint.name + ". Skipping to the next point.", creature);
                GoToNextPoint();
            }
        }
        else
        {
            stuckTimer = 0.0f;
        }
    }

    public override void Exit()
    {
        // clear the pause so the next Enter starts fresh
        isWaiting = false;
    }

    // goes to the next point (% wraps around: after the last point, go back to point 0)
    private void GoToNextPoint()
    {
        targetIndex = (targetIndex + 1) % creature.patrolPoints.Count;
        GoToCurrentPoint();
    }

    // asks the agent for a path to the current point
    private void GoToCurrentPoint()
    {
        pathChecked = false;
        stuckTimer = 0.0f;

        bool requested = creature.Agent.SetDestination(CurrentPoint.position);

        // the point is not near the NavMesh at all
        if (!requested)
        {
            Debug.LogWarning("[Patrol] " + CurrentPoint.name + " is not on the NavMesh. Skipping it.", CurrentPoint);
            StartWaiting(retryDelay);
        }
    }

    // starts a pause (at a point, or before a retry)
    private void StartWaiting(float seconds)
    {
        isWaiting = true;
        waitTimer = seconds;
    }

    // finds the closest patrol point in a straight line
    // (sqrMagnitude skips the square root, so it is cheaper and still fine for comparing)
    private int FindClosestPointIndex()
    {
        Vector3 myPosition = creature.transform.position;
        int closestIndex = 0;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < creature.patrolPoints.Count; i++)
        {
            float sqrDistance = (creature.patrolPoints[i].position - myPosition).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}
