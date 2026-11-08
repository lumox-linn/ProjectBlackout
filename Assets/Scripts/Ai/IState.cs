// ---------------------------------------------------------------------------
// IState  (Task B2)
//
// The contract every creature state must follow. The StateMachine only knows
// about this interface, so it can run ANY state without knowing what it does.
//
//   Enter() - called once when the state starts   (e.g. set speed, pick target)
//   Tick()  - called every frame while it's active (e.g. check senses, move)
//   Exit()  - called once when the state ends     (e.g. clean up, reset values)
// ---------------------------------------------------------------------------
public interface IState
{
    string Name { get; }   // shown in the debug label and the Console logs
    void Enter();
    void Tick();
    void Exit();
}
