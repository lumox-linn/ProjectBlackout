using UnityEngine;

// ---------------------------------------------------------------------------
// StateMachine  (Task B2, minimum time in state added in B8)
//
// A small, reusable finite state machine (FSM).
// - Holds exactly ONE current state.
// - Tick() runs the current state's Tick() every frame.
// - ChangeState() switches safely: old.Exit() -> new.Enter().
// - Tracks how long we've been in the current state (TimeInState).
// - B8: MinTimeInState + CanLeaveState. Normal (sense-based) transitions
//   only happen once the creature has been in its state for at least
//   MinTimeInState seconds, so it can't flicker between two states when a
//   sense result changes back and forth. Forced changes (a stun, debug keys,
//   timers that have finished) still use ChangeState() directly.
//
// It's a plain C# class, NOT a MonoBehaviour: it doesn't need to be on a
// GameObject. CreatureController creates it with "new" and calls Tick() from
// its own Update().
//
// Pattern reference (the "State" design pattern):
//   Nystrom, R. (2014) Game Programming Patterns, chapter "State".
//   https://gameprogrammingpatterns.com/state.html
// ---------------------------------------------------------------------------
public class StateMachine
{
    // The object that owns this FSM (the creature). Used in log messages, and
    // clicking a log in the Console highlights this object in the Hierarchy.
    private readonly Object owner;

    public IState CurrentState { get; private set; }
    public IState PreviousState { get; private set; }

    // Seconds spent in the current state. Reset to 0 on every change.
    public float TimeInState { get; private set; }

    // When true, every state change is written to the Console.
    public bool LogChanges { get; set; }

    // Minimum seconds in a state before a normal transition is allowed (B8).
    public float MinTimeInState { get; set; }

    // True once the current state has lasted at least MinTimeInState.
    public bool CanLeaveState => TimeInState >= MinTimeInState;

    // Other scripts can listen for state changes (old state, new state).
    // Example use later: play a growl sound when entering Chase.
    public event System.Action<IState, IState> StateChanged;

    public StateMachine(Object owner, bool logChanges = true, float minTimeInState = 0f)
    {
        this.owner = owner;
        LogChanges = logChanges;
        MinTimeInState = minTimeInState;
    }

    public void ChangeState(IState newState)
    {
        if (newState == null)
        {
            Debug.LogWarning($"[FSM] {owner.name}: tried to change to a null state.", owner);
            return;
        }

        // Already in this state: do nothing (no Exit/Enter, no log).
        if (newState == CurrentState)
        {
            return;
        }

        IState oldState = CurrentState;

        // 1) Let the old state clean up.
        if (oldState != null)
        {
            oldState.Exit();
        }

        if (LogChanges)
        {
            if (oldState == null)
            {
                Debug.Log($"[FSM] {owner.name}: enter {newState.Name}", owner);
            }
            else
            {
                Debug.Log($"[FSM] {owner.name}: exit {oldState.Name} ({TimeInState:F1} s) -> enter {newState.Name}", owner);
            }
        }

        // 2) Switch.
        PreviousState = oldState;
        CurrentState = newState;
        TimeInState = 0f;

        // 3) Let the new state set itself up.
        CurrentState.Enter();

        // 4) Tell anyone who's listening.
        StateChanged?.Invoke(oldState, newState);
    }

    // Call this once per frame (CreatureController does it in Update).
    public void Tick()
    {
        if (CurrentState == null)
        {
            return;
        }

        TimeInState += Time.deltaTime;
        CurrentState.Tick();
    }
}
