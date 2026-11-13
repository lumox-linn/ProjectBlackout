using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// the player is in reach: stop, face them and attack in swings
// each swing starts the attack animation, and the hit lands attackWindup seconds later
// (only if the player is still in reach, so they can dodge by running away)
// the creature only uses the IDamageable interface from Core, so it does not need to know the player's health script
// Attack -> Chase when the player is further than attackRange + attackRangeBuffer, or out of sight
public class AttackState : CreatureState
{
    // counts down to the next swing
    private float swingTimer = 0.0f;
    // counts down from the start of a swing to the hit
    private float hitTimer = 0.0f;
    // true when a swing has started but not landed yet
    private bool hitPending = false;
    // the player's health script
    private IDamageable target;

    public AttackState(CreatureController creature) : base(creature)
    {
    }

    public override string Name
    {
        get { return "Attack"; }
    }

    public override void Enter()
    {
        // stop moving and turn towards the player ourselves
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        // first swing straight away
        swingTimer = 0.0f;
        hitPending = false;

        // find the player's health script once (not on every hit)
        target = FindDamageable(creature.Senses.Player);
        if (target == null)
        {
            Debug.LogWarning("[Attack] The player has no IDamageable component, so hits do nothing. (Add TestPlayerHealth to the PlayerStandIn for testing.)", creature);
        }
    }

    public override void Tick()
    {
        CreatureSenses senses = creature.Senses;

        // the player got away or out of sight
        // (the buffer means it does not flicker Attack/Chase/Attack at the edge of the range)
        bool tooFar = creature.FlatDistanceToPlayer() > creature.attackRange + creature.attackRangeBuffer;
        bool playerEscaped = tooFar || !senses.CanSeePlayer;

        if (playerEscaped)
        {
            // a swing that has not landed misses
            hitPending = false;

            // Attack -> Chase
            if (creature.Fsm.CanLeaveState)
            {
                creature.Fsm.ChangeState(creature.Chase);
            }

            // not allowed to leave yet: wait, but never hit someone out of reach
            return;
        }

        FacePlayer(senses.Player);

        // a swing in progress: land the hit after the wind-up
        if (hitPending)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0.0f)
            {
                hitPending = false;
                Hit();
            }
        }

        // time for the next swing
        swingTimer -= Time.deltaTime;
        if (swingTimer <= 0.0f)
        {
            swingTimer = creature.attackCooldown;
            hitTimer = creature.attackWindup;
            hitPending = true;

            // play the attack animation
            creature.RaiseAttackSwing();
        }
    }

    public override void Exit()
    {
        // give movement and rotation back to the agent
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
        hitPending = false;
        target = null;
    }

    // damages the player through IDamageable
    private void Hit()
    {
        // maybe the health script was added since Enter
        if (target == null)
        {
            target = FindDamageable(creature.Senses.Player);
        }

        if (target != null)
        {
            target.TakeDamage(creature.attackDamage);
            Debug.Log("[Attack] Hit the player for " + creature.attackDamage + " damage.", creature);
        }
        else
        {
            Debug.Log("[Attack] Swing! (no IDamageable on the player, so no damage)", creature);
        }
    }

    // turns smoothly to face the player, only around the Y axis (stays upright)
    private void FacePlayer(Transform player)
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.position - creature.transform.position;
        direction.y = 0.0f;

        // the player is right on top of the creature
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        creature.transform.rotation = Quaternion.RotateTowards(creature.transform.rotation, targetRotation, creature.attackTurnSpeed * Time.deltaTime);
    }

    // looks for IDamageable on the player, then its children, then its parents
    // (the health script could be on any of them)
    private IDamageable FindDamageable(Transform player)
    {
        if (player == null)
        {
            return null;
        }

        IDamageable damageable = player.GetComponent<IDamageable>();
        if (damageable == null)
        {
            damageable = player.GetComponentInChildren<IDamageable>();
        }
        if (damageable == null)
        {
            damageable = player.GetComponentInParent<IDamageable>();
        }
        return damageable;
    }
}
