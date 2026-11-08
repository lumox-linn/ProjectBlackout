// ---------------------------------------------------------------------------
// PatrolState  (placeholder from B2, filled in by B3)
//
// Walks between PatrolPoint_1-5 in a loop, pausing briefly at each one.
// For now it does nothing, so the state machine can be tested with keys 1-5.
// ---------------------------------------------------------------------------
public class PatrolState : CreatureState
{
    public PatrolState(CreatureController creature) : base(creature) { }

    public override string Name => "Patrol";

    public override void Enter()
    {
        // B3
    }

    public override void Tick()
    {
        // B3
    }

    public override void Exit()
    {
        // B3
    }
}
