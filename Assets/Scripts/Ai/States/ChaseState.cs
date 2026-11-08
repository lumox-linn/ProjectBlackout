using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// ChaseState  (B4, updated in B5 and B6)
//
// Runs after the player at chase speed.
// - While the creature can SEE the player: re-target the player's position
//   every ChaseRepathInterval seconds (not every frame, which is cheaper).
// - When it loses sight: keep running to the LAST SEEN position. If it still
//   hasn't seen the player after LoseSightTime seconds -> Investigate the
//   last seen position (B5: it walks there and looks around).
//   (The short delay stops it giving up the moment the player passes a pillar.)
//
// Transitions:
//   Chase -> Investigate   lost sight for LoseSightTime seconds
//   Chase -> Attack        can see the player AND they're within attack range (B6)
// Noises are ignored while chasing: seeing the player is more important.
// ---------------------------------------------------------------------------
public class ChaseState : CreatureState
{
    private float repathTimer;

    public ChaseState(CreatureController creature) : base(creature) { }

    public override string Name => "Chase";

    public override void Enter()
    {
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.ChaseSpeed;
        agent.isStopped = false;
        repathTimer = 0f; // update the target straight away on the first Tick
    }

    public override void Tick()
    {
        CreatureSenses senses = creature.Senses;

        // Transition: Chase -> Investigate after losing sight for a while.
        if (!senses.CanSeePlayer && senses.TimeSinceLastSeen >= creature.LoseSightTime)
        {
            creature.Investigate.SetTarget(senses.LastSeenPlayerPosition, "last seen position");
            creature.Fsm.ChangeState(creature.Investigate);
            return;
        }

        // Transition: Chase -> Attack when the player is visible and close enough.
        if (senses.CanSeePlayer && creature.FlatDistanceToPlayer() <= creature.AttackRange)
        {
            creature.Fsm.ChangeState(creature.Attack);
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer > 0f)
        {
            return;
        }
        repathTimer = creature.ChaseRepathInterval;

        // Visible: go to where the player IS. Not visible: go to where they were last SEEN.
        Vector3 target = (senses.CanSeePlayer && senses.Player != null)
            ? senses.Player.position
            : senses.LastSeenPlayerPosition;

        creature.Agent.SetDestination(target);
    }

    public override void Exit()
    {
        // Nothing to clean up. The next state sets its own speed and destination.
    }
}
