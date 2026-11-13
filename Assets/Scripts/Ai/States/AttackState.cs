using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// the player is in reach: slow down, face them and attack in swings
// each swing plays the whole attack animation (attackDuration), and the hit lands when the claws strike
// (attackHitPoint of the way through), only if the player is still in reach, so they can dodge by running away
// one animation = one hit: after the animation there is a pause (attackCooldown) before the next swing,
// and leaving and coming back into Attack does not start a new swing early
// during the pause the creature keeps edging towards the player
// the agent is never stopped suddenly: it brakes with its normal acceleration, so the stop looks smooth
// the creature only uses the IDamageable interface from Core, so it does not need to know the player's health script
// Attack -> Chase when the player is further than attackRange + attackRangeBuffer, or out of sight
public class AttackState : CreatureState
{
    // the earliest time the next swing may start (kept between visits to this state,
    // so leaving Attack and coming straight back cannot start an extra swing)
    private float nextSwingTime = 0.0f;
    // counts down from the start of a swing to the hit
    private float hitTimer = 0.0f;
    // true when a swing has started but not landed yet
    private bool hitPending = false;
    // counts down while the attack animation is playing (the creature holds still)
    private float swingAnimTimer = 0.0f;
    // counts down to the next movement update during the pause
    private float moveTimer = 0.0f;
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
        // slow down to walking speed (the agent brakes smoothly instead of stopping dead)
        // and turn towards the player ourselves
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.patrolSpeed;
        agent.isStopped = false;
        agent.updateRotation = false;

        // the first swing happens as soon as the pause from the last one is over
        hitPending = false;
        swingAnimTimer = 0.0f;
        moveTimer = 0.0f;

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
        UpdateMovement(senses.Player);

        // a swing in progress: land the hit when the claws strike
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
        if (Time.time >= nextSwingTime)
        {
            // the next swing can only start after this whole animation plus the pause
            nextSwingTime = Time.time + creature.attackDuration + creature.attackCooldown;

            // the hit lands when the claws strike in the animation
            hitTimer = creature.attackDuration * Mathf.Clamp01(creature.attackHitPoint);
            hitPending = true;

            // hold still while the animation plays
            swingAnimTimer = creature.attackDuration;

            // play the attack animation
            creature.RaiseAttackSwing();
        }
    }

    public override void Exit()
    {
        // give rotation back to the agent (Chase sets its own speed and destination)
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
        hitPending = false;
        target = null;
    }

    // while swinging: brake to a stop where it is
    // during the pause: walk slowly after the player so it does not look frozen
    private void UpdateMovement(Transform player)
    {
        NavMeshAgent agent = creature.Agent;

        if (swingAnimTimer > 0.0f)
        {
            swingAnimTimer -= Time.deltaTime;

            // a destination right where it stands makes the agent brake with its normal acceleration
            if (agent.hasPath)
            {
                agent.SetDestination(creature.transform.position);
            }
            return;
        }

        // only update the path a few times per second
        moveTimer -= Time.deltaTime;
        if (moveTimer > 0.0f || player == null)
        {
            return;
        }
        moveTimer = creature.chaseRepathInterval;

        // the agent stops at its Stopping Distance, so it stays just in front of the player
        agent.SetDestination(player.position);
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
