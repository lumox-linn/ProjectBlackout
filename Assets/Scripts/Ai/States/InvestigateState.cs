using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// InvestigateState  (B5; min time in state B8; IsLookingAround B9)
//
// Goes to check a spot, looks around, and gives up if it finds nothing.
// The spot is set with SetTarget() BEFORE switching to this state:
//   - Patrol: the position of a noise it heard
//   - Chase:  the player's last seen position
//
// Two phases:
//   1. Moving:        walk to the target (investigate speed).
//   2. Looking around: stop and look RIGHT, then LEFT, then face forward again,
//                      for LookAroundTime seconds, so the view cone sweeps the
//                      area. Sight keeps being checked while it turns.
//                      Each turn is eased (slow-fast-slow) with a short hold at
//                      each side, so it looks natural and matches the
//                      LookRight/LookLeft animations (B9).
//
// Transitions:
//   Investigate -> Chase        sees the player
//   Investigate -> Investigate  hears a newer noise (new target, starts again)
//   Investigate -> Patrol       finished looking around without seeing the player
// Investigate -> Chase waits for the minimum time in state (B8).
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

    // B9: CreatureAnimation plays the look-around animation while this is true.
    public bool IsLookingAround => isActive && phase == Phase.LookingAround;

    // --- Look-around timeline (fractions of LookAroundTime) ---
    //   0.00-0.15  turn to the right      (eased)
    //   0.15-0.40  hold, looking right    (LookRight animation)
    //   0.40-0.60  turn across to the left (eased)
    //   0.60-0.85  hold, looking left     (LookLeft animation)
    //   0.85-1.00  turn back to the front (eased)
    private const float TurnRightEnd = 0.15f;
    private const float HoldRightEnd = 0.40f;
    private const float TurnLeftEnd = 0.60f;
    private const float HoldLeftEnd = 0.85f;

    // Body yaw at a point in the look-around, from -1 (full left) to +1 (full right).
    // Mathf.SmoothStep eases in and out, so the creature never starts or stops a turn suddenly.
    private static float LookCurve(float progress)
    {
        if (progress < TurnRightEnd) return Mathf.SmoothStep(0f, 1f, progress / TurnRightEnd);
        if (progress < HoldRightEnd) return 1f;
        if (progress < TurnLeftEnd) return Mathf.SmoothStep(1f, -1f, (progress - HoldRightEnd) / (TurnLeftEnd - HoldRightEnd));
        if (progress < HoldLeftEnd) return -1f;
        return Mathf.SmoothStep(-1f, 0f, (progress - HoldLeftEnd) / (1f - HoldLeftEnd));
    }

    private float LookProgress => Mathf.Clamp01(lookTimer / creature.LookAroundTime);

    // B9: which side to look at, for the LookLeft/LookRight animations:
    // +1 = look right, -1 = look left, 0 = not looking around.
    // Switches halfway through the turn from right to left.
    public int LookSide
    {
        get
        {
            if (!IsLookingAround)
            {
                return 0;
            }
            return LookProgress < 0.5f ? 1 : -1;
        }
    }

    // B9: which way the body is turning right now, from -1 (turning left) to +1 (turning right),
    // 0 while holding still. Only used by the optional procedural head turn.
    public float LookTurnDirection
    {
        get
        {
            if (!IsLookingAround)
            {
                return 0f;
            }
            const float step = 0.01f;
            float p = LookProgress;
            float slope = (LookCurve(Mathf.Min(1f, p + step)) - LookCurve(Mathf.Max(0f, p - step))) / (2f * step);
            return Mathf.Clamp(slope / 15f, -1f, 1f); // 15 = the fastest slope in the curve
        }
    }

    // Call before ChangeState(Investigate), or while already investigating
    // to switch to a new target (e.g. a newer noise).
    public void SetTarget(Vector3 position, string reason)
    {
        target = position;
        hasTarget = true;

        if (creature.LogStateDetails)
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
        if (creature.Fsm.CanLeaveState && creature.Senses.CanSeePlayer)
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

        if (creature.LogStateDetails)
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
            if (creature.LogStateDetails)
            {
                Debug.Log("[Investigate] Found nothing. Back to patrol.", creature);
            }
            creature.Fsm.ChangeState(creature.Patrol);
            return;
        }

        // Right, hold, across to the left, hold, back to the front (see LookCurve).
        float yaw = LookCurve(progress) * creature.LookAroundAngle;
        creature.transform.rotation = lookStartRotation * Quaternion.Euler(0f, yaw, 0f);
    }
}
