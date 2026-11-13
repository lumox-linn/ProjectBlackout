using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// entered through CreatureController.Stun() (IStunnable), usually when a thrown crate hits the creature hard
// it stops, ignores its senses and wobbles, then investigates where the player was when it got hit
// Stunned -> Investigate when the stun timer ends (and the stun immunity starts)
public class StunnedState : CreatureState
{
    // how far (degrees) and how fast it sways while stunned
    private const float wobbleAngle = 12.0f;
    private const float wobbleSpeed = 10.0f;

    // how long the stun lasts (set by the crate that hit it)
    private float duration = 3.0f;
    // counts down the stun
    private float timer = 0.0f;
    // where the player was when the creature got hit (roughly where the crate came from)
    private Vector3 playerPositionWhenHit;
    // the rotation before the wobble
    private Quaternion startRotation;

    public StunnedState(CreatureController creature) : base(creature)
    {
    }

    public override string Name
    {
        get { return "Stunned"; }
    }

    // called by CreatureController.Stun() before switching to this state
    public void SetDuration(float seconds)
    {
        duration = seconds;
    }

    public override void Enter()
    {
        // stop moving and turn the creature ourselves
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        timer = duration;
        startRotation = creature.transform.rotation;

        // remember where the player was
        CreatureSenses senses = creature.Senses;
        if (senses.Player != null)
        {
            playerPositionWhenHit = senses.Player.position;
        }
        else
        {
            playerPositionWhenHit = creature.transform.position;
        }
    }

    public override void Tick()
    {
        // no sight or hearing checks here: a stunned creature does not react
        timer -= Time.deltaTime;

        // sway left and right around the Y axis
        float yaw = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAngle;
        creature.transform.rotation = startRotation * Quaternion.Euler(0.0f, yaw, 0.0f);

        // Stunned -> Investigate when the time is up
        if (timer <= 0.0f)
        {
            creature.StartStunImmunity();
            creature.Investigate.SetTarget(playerPositionWhenHit, "spot the hit came from");
            creature.Fsm.ChangeState(creature.Investigate);
        }
    }

    public override void Exit()
    {
        // stop mid-wobble cleanly
        creature.transform.rotation = startRotation;

        // give movement and rotation back to the agent
        NavMeshAgent agent = creature.Agent;
        agent.isStopped = false;
        agent.updateRotation = true;
    }
}
