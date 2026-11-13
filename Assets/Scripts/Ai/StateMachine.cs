using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// a small finite state machine (FSM) that holds one current state and switches between states
// (plain C# class, not a MonoBehaviour: CreatureController creates it and calls Tick() from its Update)
// pattern reference: Nystrom, R. (2014) Game Programming Patterns, chapter "State"
// https://gameprogrammingpatterns.com/state.html
public class StateMachine
{
    // write every state change to the Console
    public bool logChanges = true;
    // minimum seconds in a state before a sense-based transition is allowed (stops flickering)
    public float minTimeInState = 0.0f;

    // the creature that owns this state machine (clicking a log highlights it in the Hierarchy)
    private Object owner;
    // the state that is running now, and the one before it
    private IState currentState;
    private IState previousState;
    // seconds spent in the current state (reset to 0 on every change)
    private float timeInState = 0.0f;

    // lets other scripts listen for state changes (old state, new state)
    public event System.Action<IState, IState> StateChanged;

    // lets other scripts read the states and the time (but not change them)
    public IState CurrentState
    {
        get { return currentState; }
    }

    public IState PreviousState
    {
        get { return previousState; }
    }

    public float TimeInState
    {
        get { return timeInState; }
    }

    // true once the current state has lasted at least minTimeInState
    // (sense-based transitions check this; stuns, debug keys and finished timers do not)
    public bool CanLeaveState
    {
        get { return timeInState >= minTimeInState; }
    }

    // creates the state machine for a creature
    public StateMachine(Object owner, bool logChanges, float minTimeInState)
    {
        this.owner = owner;
        this.logChanges = logChanges;
        this.minTimeInState = minTimeInState;
    }

    // switches to a new state: old state Exit() -> new state Enter()
    public void ChangeState(IState newState)
    {
        // nothing to switch to
        if (newState == null)
        {
            Debug.LogWarning("[FSM] " + owner.name + ": tried to change to a null state.", owner);
            return;
        }

        // already in this state: do nothing (no Exit/Enter, no log)
        if (newState == currentState)
        {
            return;
        }

        IState oldState = currentState;

        // 1) let the old state clean up
        if (oldState != null)
        {
            oldState.Exit();
        }

        // log the change
        if (logChanges)
        {
            if (oldState == null)
            {
                Debug.Log("[FSM] " + owner.name + ": enter " + newState.Name, owner);
            }
            else
            {
                Debug.Log("[FSM] " + owner.name + ": exit " + oldState.Name + " (" + timeInState.ToString("0.0") + " s) -> enter " + newState.Name, owner);
            }
        }

        // 2) switch
        previousState = oldState;
        currentState = newState;
        timeInState = 0.0f;

        // 3) let the new state set itself up
        currentState.Enter();

        // 4) tell anyone who is listening
        if (StateChanged != null)
        {
            StateChanged(oldState, newState);
        }
    }

    // called once per frame by CreatureController
    public void Tick()
    {
        // no state yet
        if (currentState == null)
        {
            return;
        }

        // count the time in this state and run it
        timeInState += Time.deltaTime;
        currentState.Tick();
    }
}
