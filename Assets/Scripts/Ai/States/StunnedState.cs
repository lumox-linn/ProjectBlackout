using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// StunnedState  (B7)
//
// Entered through CreatureController.Stun(seconds) (IStunnable), usually when
// a thrown crate hits the creature hard.
// - Enter: stop moving and remember where the player was (the creature knows
//          roughly where the hit came from).
// - Tick:  ignore the senses, count down, wobble a little so it LOOKS stunned.
// - When the time is up: start stun immunity, then Investigate the spot where
//          the player was when it got hit.
// - Exit:  give movement and rotation back to the NavMeshAgent.
//
// Transitions:
//   Any state -> Stunned     Stun() called and not immune (CreatureController)
//   Stunned   -> Investigate stun timer finished
// ---------------------------------------------------------------------------
public class StunnedState : CreatureState
{
    // Wobble: how far (degrees) and how fast it sways while stunned.
    private const float WobbleAngle = 12f;
    private const float WobbleSpeed = 10f;

    private float duration = 3f;
    private float timer;
    private Vector3 playerPositionWhenHit;
    private Quaternion startRotation;

    public StunnedState(CreatureController creature) : base(creature) { }

    public override string Name => "Stunned";

    // Called by CreatureController.Stun() before switching to this state.
    public void SetDuration(float seconds)
    {
        duration = seconds;
    }

    public override void Enter()
    {
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        timer = duration;
        startRotation = creature.transform.rotation;

        CreatureSenses senses = creature.Senses;
        playerPositionWhenHit = senses.Player != null
            ? senses.Player.position
            : creature.transform.position;
    }

    public override void Tick()
    {
        // No sight or hearing checks here: a stunned creature doesn't react.
        timer -= Time.deltaTime;

        // Sway left and right around the Y axis.
        float yaw = Mathf.Sin(Time.time * WobbleSpeed) * WobbleAngle;
        creature.transform.rotation = startRotation * Quaternion.Euler(0f, yaw, 0f);

        if (timer <= 0f)
        {
            creature.StartStunImmunity();
            creature.Investigate.SetTarget(playerPositionWhenHit, "spot the hit came from");
            creature.Fsm.ChangeState(creature.Investigate);
        }
    }

    public override void Exit()
    {
        creature.transform.rotation = startRotation; // stop mid-wobble cleanly

        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
    }
}
