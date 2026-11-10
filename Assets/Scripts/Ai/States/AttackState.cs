using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// AttackState  (B6; min time in state B8; swing/hit timing B9)
//
// The player is in reach: stop, face them, and attack in swings.
// Each swing:
//   1. starts -> AttackSwing event (CreatureAnimation plays the attack animation)
//   2. AttackWindup seconds later the hit lands: IDamageable.TakeDamage(AttackDamage)
//      (only if the player is still in reach, so they can dodge by running)
//   3. the next swing starts AttackCooldown seconds after the previous one started
// The first swing starts as soon as Attack is entered.
//
// The creature never needs to know about the player's health script:
// it only uses the IDamageable interface from Core (Eliza's PlayerHealth,
// or TestPlayerHealth while testing, implements it).
//
// Transitions:
//   Attack -> Chase   player further than AttackRange + AttackRangeBuffer,
//                     or out of sight (Chase then handles losing them).
//                     Waits for the minimum time in state (B8).
// ---------------------------------------------------------------------------
public class AttackState : CreatureState
{
    private float swingTimer;   // counts down to the next swing
    private float hitTimer;     // counts down from swing start to the hit
    private bool hitPending;    // a swing has started and hasn't landed yet
    private IDamageable target;

    public AttackState(CreatureController creature) : base(creature) { }

    public override string Name => "Attack";

    public override void Enter()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false; // we turn the creature towards the player ourselves

        swingTimer = 0f;              // first swing straight away
        hitPending = false;
        target = FindDamageable(creature.Senses.Player);

        if (target == null)
        {
            Debug.LogWarning("[Attack] The player has no IDamageable component, so hits do nothing. " +
                             "(Add TestPlayerHealth to the PlayerStandIn for testing.)", creature);
        }
    }

    public override void Tick()
    {
        CreatureSenses senses = creature.Senses;

        // Transition: Attack -> Chase when the player gets away or out of sight.
        // The buffer means it doesn't flicker Attack/Chase/Attack at the edge of the range.
        bool tooFar = creature.FlatDistanceToPlayer() > creature.AttackRange + creature.AttackRangeBuffer;
        bool playerEscaped = tooFar || !senses.CanSeePlayer;

        if (playerEscaped)
        {
            hitPending = false; // a swing that hasn't landed misses
            if (creature.Fsm.CanLeaveState)
            {
                creature.Fsm.ChangeState(creature.Chase);
            }
            // Not allowed to leave yet (B8): wait, but never hit someone out of reach.
            return;
        }

        FacePlayer(senses.Player);

        // A swing in progress: land the hit after the wind-up.
        if (hitPending)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f)
            {
                hitPending = false;
                Hit();
            }
        }

        // Time for the next swing?
        swingTimer -= Time.deltaTime;
        if (swingTimer <= 0f)
        {
            swingTimer = creature.AttackCooldown;
            hitTimer = creature.AttackWindup;
            hitPending = true;
            creature.RaiseAttackSwing(); // B9: play the attack animation
        }
    }

    public override void Exit()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
        hitPending = false;
        target = null;
    }

    // --- Helpers ---

    private void Hit()
    {
        if (target == null)
        {
            target = FindDamageable(creature.Senses.Player); // maybe it was added since
        }

        if (target != null)
        {
            target.TakeDamage(creature.AttackDamage);
            Debug.Log($"[Attack] Hit the player for {creature.AttackDamage} damage.", creature);
        }
        else
        {
            Debug.Log("[Attack] Swing! (no IDamageable on the player, so no damage)", creature);
        }
    }

    // Turn smoothly to face the player, only around the Y axis (stay upright).
    private void FacePlayer(Transform player)
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.position - creature.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        creature.transform.rotation = Quaternion.RotateTowards(
            creature.transform.rotation, targetRotation, creature.AttackTurnSpeed * Time.deltaTime);
    }

    // Looks for IDamageable on the player object, then its children, then its parents
    // (the health script could be on any of them).
    private static IDamageable FindDamageable(Transform player)
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
