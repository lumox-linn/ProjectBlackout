using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// the player is in reach: swing at them without stopping
// each swing plays the whole attack animation (attackDuration), and the hit lands when the claws strike
// (attackHitPoint of the way through), only if the player is still in reach, so they can dodge by sprinting away
// wind-up (before the hit): the creature keeps running after the player at chase speed,
// so a walking player cannot just walk out of reach (the swing is on an upper-body layer, so the legs keep running)
// after a hit: it stands still to finish the swing, then waits attackCooldown before the next swing
// after a miss: no extra pause, it can swing again as soon as the animation is over
// leaving and coming back into Attack does not start a new swing early
// the creature only uses the IDamageable interface from Core, so it does not need to know the player's health script
// Attack -> Chase when the player is out of sight, or out of reach with no swing on the way
public class AttackState : CreatureState
{
    // the earliest time the next swing may start (kept between visits to this state,
    // so leaving Attack and coming straight back cannot start an extra swing)
    private float nextSwingTime = 0.0f;
    // counts down from the start of a swing to the hit
    private float hitTimer = 0.0f;
    // true when a swing has started but not landed yet
    private bool hitPending = false;
    // counts down while the creature stands still after a hit, finishing the swing
    private float recoverTimer = 0.0f;
    // counts down to the next path update
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
        // keep running at chase speed, and turn towards the player ourselves
        NavMeshAgent agent = creature.Agent;
        agent.speed = creature.chaseSpeed;
        agent.isStopped = false;
        agent.updateRotation = false;

        // the first swing happens as soon as the pause from the last one is over
        hitPending = false;
        recoverTimer = 0.0f;
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
        Transform player = senses.Player;

        // after a hit: stand still and finish the swing before doing anything else
        // (this is the player's chance to get away)
        if (recoverTimer > 0.0f)
        {
            recoverTimer -= Time.deltaTime;
            FacePlayer(player);
            Brake();
            return;
        }

        // out of reach (the buffer means it does not flicker Attack/Chase/Attack at the edge of the range)
        bool tooFar = creature.FlatDistanceToPlayer() > creature.attackRange + creature.attackRangeBuffer;

        // the player got away: out of sight, or out of reach with no swing on the way
        // (during a wind-up it keeps going, the swing only misses if they are still out of reach at the hit)
        if (!senses.CanSeePlayer || (tooFar && !hitPending))
        {
            // a swing that has not landed misses
            hitPending = false;

            // Attack -> Chase
            if (creature.Fsm.CanLeaveState)
            {
                creature.Fsm.ChangeState(creature.Chase);
            }
            return;
        }

        FacePlayer(player);
        FollowPlayer(player);

        // a swing in progress: the claws strike now
        if (hitPending)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0.0f)
            {
                hitPending = false;
                if (tooFar)
                {
                    Miss();
                }
                else
                {
                    Hit();
                }
            }
            return;
        }

        // time for the next swing
        if (Time.time >= nextSwingTime)
        {
            StartSwing();
        }
    }

    public override void Exit()
    {
        // give rotation back to the agent (the next state sets its own speed and destination)
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
        hitPending = false;
        recoverTimer = 0.0f;
        target = null;
    }

    // starts one swing: plays the animation and times the hit
    private void StartSwing()
    {
        // no new swing before this animation is over (a hit adds the pause on top)
        nextSwingTime = Time.time + creature.attackDuration;

        // the hit lands when the claws strike in the animation
        hitTimer = creature.attackDuration * Mathf.Clamp01(creature.attackHitPoint);
        hitPending = true;

        // play the attack animation
        creature.RaiseAttackSwing();
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
        }

        // stand still for the rest of the animation, then wait the pause before the next swing
        recoverTimer = creature.attackDuration * (1.0f - Mathf.Clamp01(creature.attackHitPoint));
        nextSwingTime = nextSwingTime + creature.attackCooldown;

        if (creature.logStateDetails)
        {
            if (target != null)
            {
                Debug.Log("[Attack] Hit the player for " + creature.attackDamage + " damage.", creature);
            }
            else
            {
                Debug.Log("[Attack] Swing! (no IDamageable on the player, so no damage)", creature);
            }
        }
    }

    // the player was out of reach when the claws struck
    private void Miss()
    {
        if (creature.logStateDetails)
        {
            Debug.Log("[Attack] Missed, the player got out of reach (" + creature.FlatDistanceToPlayer().ToString("0.0") + " m).", creature);
        }
    }

    // runs after the player at chase speed (it stops at its Stopping Distance, just in front of them)
    private void FollowPlayer(Transform player)
    {
        // only update the path a few times per second
        moveTimer -= Time.deltaTime;
        if (moveTimer > 0.0f || player == null)
        {
            return;
        }
        moveTimer = creature.chaseRepathInterval;

        creature.Agent.SetDestination(player.position);
    }

    // a destination right where it stands makes the agent brake with its normal acceleration
    // (smoother than stopping it dead)
    private void Brake()
    {
        NavMeshAgent agent = creature.Agent;
        if (agent.hasPath)
        {
            agent.SetDestination(creature.transform.position);
        }
        // follow again straight away when the recovery ends
        moveTimer = 0.0f;
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
