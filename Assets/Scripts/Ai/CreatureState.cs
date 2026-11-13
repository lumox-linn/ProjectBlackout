using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// base class for the 5 creature states
// gives every state a reference to the CreatureController, so it can reach the agent, the senses and the other states
// (Enter, Tick and Exit are empty here, so each state only overrides the ones it needs)
public abstract class CreatureState : IState
{
    // the creature this state belongs to
    protected CreatureController creature;

    // called by each state's constructor
    protected CreatureState(CreatureController creature)
    {
        this.creature = creature;
    }

    // each state gives its own name
    public abstract string Name { get; }

    public virtual void Enter()
    {
    }

    public virtual void Tick()
    {
    }

    public virtual void Exit()
    {
    }
}
