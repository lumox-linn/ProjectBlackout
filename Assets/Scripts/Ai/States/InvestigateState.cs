using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// goes to check a spot, looks right and left, and gives up if it finds nothing
// the spot is set with SetTarget() before switching here: a noise (from Patrol),
// the last seen position (from Chase) or where the player was when it got stunned
// Investigate -> Chase when it sees the player
// Investigate -> Investigate when it hears a newer noise (new target)
// Investigate -> Patrol when it has finished looking around without seeing the player
public class InvestigateState : CreatureState
{
    // the two parts of investigating
    private enum Phase
    {
        Moving,
        LookingAround
    }

    // how close (beyond the agent's Stopping Distance) counts as "arrived"
    private const float arriveTolerance = 0.3f;
    // slower than this (m/s) counts as "not moving"
    private const float stuckSpeed = 0.1f;

    // the look-around timeline (fractions of lookAroundTime):
    // 0.00-0.15 turn right, 0.15-0.40 hold looking right, 0.40-0.60 turn across to the left,
    // 0.60-0.85 hold looking left, 0.85-1.00 turn back to the front
    private const float turnRightEnd = 0.15f;
    private const float holdRightEnd = 0.40f;
    private const float turnLeftEnd = 0.60f;
    private const float holdLeftEnd = 0.85f;

    // the spot to check
    private Vector3 target;
    // whether a spot has been given
    private bool hasTarget = false;
    // true between Enter and Exit
    private bool isActive = false;
    // moving or looking around
    private Phase phase = Phase.Moving;
    // time spent looking around
    private float lookTimer = 0.0f;
    // seconds spent not moving on the way
    private float stuckTimer = 0.0f;
    // the rotation when looking around started
    private Quaternion lookStartRotation;

    public InvestigateState(CreatureController creature) : base(creature)
    {
    }

    public override string Name
    {
        get { return "Investigate"; }
    }

    // true while looking around (CreatureAnimation uses it)
    public bool IsLookingAround
    {
        get { return isActive && phase == Phase.LookingAround; }
    }

    // how far through the look-around we are (0 to 1)
    private float LookProgress
    {
        get { return Mathf.Clamp01(lookTimer / creature.lookAroundTime); }
    }

    // which side to look at for the LookLeft/LookRight animations
    // (+1 = look right, -1 = look left, 0 = not looking around; switches halfway through the turn)
    public int LookSide
    {
        get
        {
            if (!IsLookingAround)
            {
                return 0;
            }

            if (LookProgress < 0.5f)
            {
                return 1;
            }
            return -1;
        }
    }

    // which way the body is turning right now (-1 = left, +1 = right, 0 = holding still)
    // only used by the optional procedural head turn in CreatureAnimation
    public float LookTurnDirection
    {
        get
        {
            if (!IsLookingAround)
            {
                return 0.0f;
            }

            // how fast the curve is changing here (15 = the fastest slope in the curve)
            float step = 0.01f;
            float progress = LookProgress;
            float slope = (LookCurve(Mathf.Min(1.0f, progress + step)) - LookCurve(Mathf.Max(0.0f, progress - step))) / (2.0f * step);
            return Mathf.Clamp(slope / 15.0f, -1.0f, 1.0f);
        }
    }

    // called before ChangeState(Investigate), or while already investigating to switch to a new spot
    public void SetTarget(Vector3 position, string reason)
    {
        target = position;
        hasTarget = true;

        if (creature.logStateDetails)
        {
            Debug.Log("[Investigate] Going to check the " + reason + ".", creature);
        }

        // already investigating: go to the new spot straight away
        if (isActive)
        {
            StartMoving();
        }
    }

    public override void Enter()
    {
        isActive = true;

        // no spot given (e.g. debug key 2): use the last seen position, or look around right here
        if (!hasTarget)
        {
            if (creature.Senses.HasSeenPlayer)
            {
                target = creature.Senses.LastSeenPlayerPosition;
            }
            else
            {
                target = creature.transform.position;
            }
            hasTarget = true;
        }

        StartMoving();
    }

    public override void Tick()
    {
        // Investigate -> Chase when the creature sees the player
        if (creature.Fsm.CanLeaveState && creature.Senses.CanSeePlayer)
        {
            creature.Fsm.ChangeState(creature.Chase);
            return;
        }

        // Investigate -> Investigate when it hears a newer noise
        Vector3 noisePosition;
        if (creature.Senses.TryConsumeNoise(out noisePosition))
        {
            SetTarget(noisePosition, "new noise");
            return;
        }

        // walk there, or look around
        if (phase == Phase.Moving)
        {
            TickMoving();
        }
        else
        {
            TickLookingAround();
        }
    }

    public override void Exit()
    {
        isActive = false;
        hasTarget = false;

        // give rotation back to the agent and let it move again
        NavMeshAgent agent = creature.Agent;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    // phase 1: start walking to the spot
    private void StartMoving()
    {
        NavMeshAgent agent = creature.Agent;
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.speed = creature.investigateSpeed;

        phase = Phase.Moving;
        stuckTimer = 0.0f;

        // the spot is not near the NavMesh: just look around where we are
        if (!agent.SetDestination(target))
        {
            StartLookingAround();
        }
    }

    // phase 1: walking to the spot
    private void TickMoving()
    {
        NavMeshAgent agent = creature.Agent;

        // Unity is still calculating the path
        if (agent.pathPending)
        {
            return;
        }

        // can't get there at all: look around from here instead
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            StartLookingAround();
            return;
        }

        // arrived (or as close as a closed door allows)
        if (agent.remainingDistance <= agent.stoppingDistance + arriveTolerance)
        {
            StartLookingAround();
            return;
        }

        // stuck on the way: look around from here
        if (agent.velocity.magnitude < stuckSpeed)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= creature.stuckTimeout)
            {
                StartLookingAround();
            }
        }
        else
        {
            stuckTimer = 0.0f;
        }
    }

    // phase 2: stop and start looking around
    private void StartLookingAround()
    {
        // stop moving and turn the creature ourselves
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        phase = Phase.LookingAround;
        lookTimer = 0.0f;
        lookStartRotation = creature.transform.rotation;

        if (creature.logStateDetails)
        {
            Debug.Log("[Investigate] Looking around...", creature);
        }
    }

    // phase 2: turning right, then left, then back to the front
    private void TickLookingAround()
    {
        lookTimer += Time.deltaTime;
        float progress = lookTimer / creature.lookAroundTime;

        // Investigate -> Patrol when it has finished looking around
        if (progress >= 1.0f)
        {
            if (creature.logStateDetails)
            {
                Debug.Log("[Investigate] Found nothing. Back to patrol.", creature);
            }
            creature.Fsm.ChangeState(creature.Patrol);
            return;
        }

        // turn the body along the look-around curve
        float yaw = LookCurve(progress) * creature.lookAroundAngle;
        creature.transform.rotation = lookStartRotation * Quaternion.Euler(0.0f, yaw, 0.0f);
    }

    // the body's turn at a point in the look-around, from -1 (full left) to +1 (full right)
    // (Mathf.SmoothStep eases in and out, so a turn never starts or stops suddenly)
    private float LookCurve(float progress)
    {
        if (progress < turnRightEnd)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, progress / turnRightEnd);
        }
        if (progress < holdRightEnd)
        {
            return 1.0f;
        }
        if (progress < turnLeftEnd)
        {
            return Mathf.SmoothStep(1.0f, -1.0f, (progress - holdRightEnd) / (turnLeftEnd - holdRightEnd));
        }
        if (progress < holdLeftEnd)
        {
            return -1.0f;
        }
        return Mathf.SmoothStep(-1.0f, 0.0f, (progress - holdLeftEnd) / (1.0f - holdLeftEnd));
    }
}
