using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// InvestigateState  (B5)
//
// Goes to check a spot, looks around, and gives up if it finds nothing.
// The spot is set with SetTarget() BEFORE switching to this state:
//   - Patrol: the position of a noise it heard
//   - Chase:  the player's last seen position
//
// Two phases:
//   1. Moving:        walk to the target (investigate speed).
//   2. Looking around: stop and turn left and right (a smooth sine sweep)
//                      for LookAroundTime seconds, so the view cone sweeps
//                      the area. Sight keeps being checked while it turns.
//
// Transitions:
//   Investigate -> Chase        sees the player
//   Investigate -> Investigate  hears a newer noise (new target, starts again)
//   Investigate -> Patrol       finished looking around without seeing the player
// ---------------------------------------------------------------------------
public class InvestigateState : CreatureState
{
    private const float ArriveTolerance = 0.3f;
    private const float StuckSpeed = 0.1f;

    private enum Phase { Moving, LookingAround }

    private Vector3 target;
    private bool hasTarget;
    private bool isActive;           // true between Enter and Exit
    private Phase phase;
    private float lookTimer;
    private float stuckTimer;
    private Quaternion lookStartRotation;

    public InvestigateState(CreatureController creature) : base(creature) { }

    public override string Name => "Investigate";

    // Call before ChangeState(Investigate), or while already investigating
    // to switch to a new target (e.g. a newer noise).
    public void SetTarget(Vector3 position, string reason)
    {
        target = position;
        hasTarget = true;

        if (creature.LogPatrolPoints)
        {
            Debug.Log($"[Investigate] Going to check the {reason}.", creature);
        }

        if (isActive)
        {
            StartMoving();
        }
    }

    public override void Enter()
    {
        isActive = true;

        // No target given (e.g. forced with debug key 2): use the last seen
        // position if there is one, otherwise just look around right here.
        if (!hasTarget)
        {
            target = creature.Senses.HasSeenPlayer
                ? creature.Senses.LastSeenPlayerPosition
                : creature.transform.position;
            hasTarget = true;
        }

        StartMoving();
    }

    public override void Tick()
    {
        // Transition: Investigate -> Chase.
        if (creature.Senses.CanSeePlayer)
        {
            creature.Fsm.ChangeState(creature.Chase);
            return;
        }

        // Transition: Investigate -> Investigate (newer noise = new target).
        if (creature.Senses.TryConsumeNoise(out Vector3 noisePosition))
        {
            SetTarget(noisePosition, "new noise");
            return;
        }

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

        // Give rotation back to the NavMeshAgent and let it move again.
        NavMeshAgent agent = creature.Agent;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    // --- Phase 1: walking to the target ---

    private void StartMoving()
    {
        NavMeshAgent agent = creature.Agent;
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.speed = creature.InvestigateSpeed;

        phase = Phase.Moving;
        stuckTimer = 0f;

        if (!agent.SetDestination(target))
        {
            // Target isn't near the NavMesh: just look around where we are.
            StartLookingAround();
        }
    }

    private void TickMoving()
    {
        NavMeshAgent agent = creature.Agent;

        if (agent.pathPending)
        {
            return;
        }

        // Can't get there at all: look around from here instead.
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            StartLookingAround();
            return;
        }

        // Arrived (or as close as a closed door allows: PathPartial ends at the door).
        if (agent.remainingDistance <= agent.stoppingDistance + ArriveTolerance)
        {
            StartLookingAround();
            return;
        }

        // Stuck on the way: look around from here.
        if (agent.velocity.magnitude < StuckSpeed)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= creature.StuckTimeout)
            {
                StartLookingAround();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // --- Phase 2: looking around ---

    private void StartLookingAround()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false; // we turn the creature ourselves now

        phase = Phase.LookingAround;
        lookTimer = 0f;
        lookStartRotation = creature.transform.rotation;

        if (creature.LogPatrolPoints)
        {
            Debug.Log("[Investigate] Looking around...", creature);
        }
    }

    private void TickLookingAround()
    {
        lookTimer += Time.deltaTime;
        float progress = lookTimer / creature.LookAroundTime; // 0 -> 1

        if (progress >= 1f)
        {
            if (creature.LogPatrolPoints)
            {
                Debug.Log("[Investigate] Found nothing. Back to patrol.", creature);
            }
            creature.Fsm.ChangeState(creature.Patrol);
            return;
        }

        // Sine sweep: 0 -> +angle -> 0 -> -angle -> 0 over the look-around time.
        float yaw = Mathf.Sin(progress * Mathf.PI * 2f) * creature.LookAroundAngle;
        creature.transform.rotation = lookStartRotation * Quaternion.Euler(0f, yaw, 0f);
    }
}
