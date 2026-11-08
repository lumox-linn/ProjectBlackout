using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// AttackState  (B6, min time in state added in B8)
//
// The player is in reach: stop, face them, and hit them on a timer.
// - Enter: stop moving, take over rotation, start the wind-up timer.
// - Tick:  turn towards the player; when the timer runs out, hit:
//          IDamageable.TakeDamage(AttackDamage), then wait AttackCooldown.
// - Exit:  give movement and rotation back to the NavMeshAgent.
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
    private float attackTimer;
    private IDamageable target;

    public AttackState(CreatureController creature) : base(creature) { }

    public override string Name => "Attack";

    public override void Enter()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false; // we turn the creature towards the player ourselves

        attackTimer = creature.AttackWindup;
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
            if (creature.Fsm.CanLeaveState)
            {
                creature.Fsm.ChangeState(creature.Chase);
            }
            // Not allowed to leave yet (B8): wait, but never hit someone out of reach.
            return;
        }

        FacePlayer(senses.Player);

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            Hit();
            attackTimer = creature.AttackCooldown;
        }
    }

    public override void Exit()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
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
