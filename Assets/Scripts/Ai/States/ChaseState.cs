using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// runs after the player at chase speed
// while it can see the player it goes to where the player is; when it loses sight it goes to the last seen position
// Chase -> Attack when the player is visible and close enough
// Chase -> Investigate when the player has not been seen for loseSightTime seconds
// (noises are ignored while chasing, because seeing the player is more important)
public class ChaseState : CreatureState
{
    // counts down to the next path update
    private float repathTimer = 0.0f;

    public ChaseState(CreatureController creature) : base(creature)
    {
    }

    public override string Name
    {
        get { return "Chase"; }
    }

    public override void Enter()
    {
        // run at chase speed
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.chaseSpeed;
        agent.isStopped = false;

        // update the target straight away on the first Tick
        repathTimer = 0.0f;
    }

    public override void Tick()
    {
        CreatureSenses senses = creature.Senses;
        bool canLeave = creature.Fsm.CanLeaveState;

        // Chase -> Investigate after losing sight for a while (go to the last seen position)
        if (canLeave && !senses.CanSeePlayer && senses.TimeSinceLastSeen >= creature.loseSightTime)
        {
            creature.Investigate.SetTarget(senses.LastSeenPlayerPosition, "last seen position");
            creature.Fsm.ChangeState(creature.Investigate);
            return;
        }

        // Chase -> Attack when the player is visible and close enough
        if (canLeave && senses.CanSeePlayer && creature.FlatDistanceToPlayer() <= creature.attackRange)
        {
            creature.Fsm.ChangeState(creature.Attack);
            return;
        }

        // only update the path a few times per second (cheaper than every frame)
        repathTimer -= Time.deltaTime;
        if (repathTimer > 0.0f)
        {
            return;
        }
        repathTimer = creature.chaseRepathInterval;

        // visible: go to where the player is; not visible: go to where they were last seen
        Vector3 target;
        if (senses.CanSeePlayer && senses.Player != null)
        {
            target = senses.Player.position;
        }
        else
        {
            target = senses.LastSeenPlayerPosition;
        }

        creature.Agent.SetDestination(target);
    }

    public override void Exit()
    {
        // nothing to clean up (the next state sets its own speed and destination)
    }
}
